using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaEkg.Application.Analysis;
using SaEkg.Application.Csv;
using SaEkg.Application.Ecg;
using SaEkg.Domain.Entities;
using SaEkg.Domain.Enums;
using SaEkg.Infrastructure.Persistence;

namespace SaEkg.Infrastructure.Services;

public sealed class EcgAnalysisService(
    SaEkgDbContext db,
    IEcgSignalStore signalStore,
    ILogger<EcgAnalysisService> logger) : IEcgAnalysisService
{
    public async Task EnqueueAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        db.BackgroundJobs.Add(new BackgroundJob
        {
            Id = Guid.NewGuid(),
            Type = "AnalyzeRecord",
            PayloadJson = JsonSerializer.Serialize(new { recordId }),
            Status = "Queued",
            Attempts = 0,
            RunAfter = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReanalyzeAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        var record = await db.EcgRecords.Include(x => x.AnalysisReport)
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken)
            ?? throw new EcgUploadException("NOT_FOUND", "Запись не найдена.");

        if (record.RawPurged)
        {
            throw new EcgUploadException("VALIDATION_ERROR", "Сырые отсчёты недоступны для повторного анализа.");
        }

        if (record.AnalysisReport is not null)
        {
            db.Anomalies.RemoveRange(db.Anomalies.Where(a => a.ReportId == record.AnalysisReport.Id));
            db.AnalysisReports.Remove(record.AnalysisReport);
        }

        await signalStore.DeleteFilteredAndFiducialsAsync(recordId, cancellationToken);
        record.Status = EcgRecordStatus.Queued;
        record.FailureCode = null;
        await db.SaveChangesAsync(cancellationToken);
        await EnqueueAsync(recordId, cancellationToken);
    }

    public async Task AnalyzeRecordAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        var record = await db.EcgRecords
            .Include(x => x.Gaps)
            .Include(x => x.Patient).ThenInclude(p => p!.MedicalCard)
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken);

        if (record is null)
        {
            return;
        }

        var sex = record.Patient.MedicalCard?.Sex;
        if (string.IsNullOrWhiteSpace(sex))
        {
            record.Status = EcgRecordStatus.Failed;
            record.FailureCode = "PATIENT_SEX_REQUIRED";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        record.Status = EcgRecordStatus.Processing;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var raw = await signalStore.ReadRawSamplesAsync(recordId, cancellationToken);
            var gaps = record.Gaps
                .Select(g => new CsvGapInfo(g.StartOffsetMs, g.EndOffsetMs, g.Reason))
                .ToList();

            var result = EcgAnalyzer.Analyze(raw, gaps, sex);

            await signalStore.DeleteFilteredAndFiducialsAsync(recordId, cancellationToken);
            const int batch = 50_000;
            for (var i = 0; i < result.FilteredSamples.Count; i += batch)
            {
                var chunk = result.FilteredSamples.Skip(i).Take(batch)
                    .Select(f => new FilteredSamplePoint(f.OffsetMs, f.ValueUv))
                    .ToList();
                await signalStore.AppendFilteredSamplesAsync(recordId, chunk, cancellationToken);
            }

            await signalStore.AppendFiducialsAsync(
                recordId,
                result.Fiducials.Select(f => new FiducialSamplePoint(
                    f.CycleIdx, f.SegmentIdx, f.RMs, f.QOnMs, f.SOffMs, f.POnMs, f.POffMs, f.TOffMs, f.JMs, f.BaselineUv, f.RAmpUv)).ToList(),
                cancellationToken);

            var existing = await db.AnalysisReports.FirstOrDefaultAsync(x => x.RecordId == recordId, cancellationToken);
            if (existing is not null)
            {
                db.Anomalies.RemoveRange(db.Anomalies.Where(a => a.ReportId == existing.Id));
                db.AnalysisReports.Remove(existing);
                await db.SaveChangesAsync(cancellationToken);
            }

            var report = new AnalysisReport
            {
                Id = Guid.NewGuid(),
                RecordId = recordId,
                CreatedAt = DateTimeOffset.UtcNow,
                Status = ReportStatus.Pending,
                SummaryJson = JsonSerializer.Serialize(result.Summary)
            };
            db.AnalysisReports.Add(report);
            foreach (var a in result.Anomalies)
            {
                db.Anomalies.Add(new Anomaly
                {
                    Id = Guid.NewGuid(),
                    ReportId = report.Id,
                    IndicatorCode = a.IndicatorCode,
                    Direction = a.Direction,
                    StartOffsetMs = a.StartOffsetMs,
                    EndOffsetMs = a.EndOffsetMs,
                    ValueMin = a.ValueMin is null ? null : (decimal)a.ValueMin.Value,
                    ValueMax = a.ValueMax is null ? null : (decimal)a.ValueMax.Value,
                    Unit = a.Unit,
                    ReferenceText = a.ReferenceText,
                    CycleCount = a.CycleCount
                });
            }

            record.Status = EcgRecordStatus.Done;
            record.FilteredPurgeAt = DateTimeOffset.UtcNow.AddYears(10);
            record.FailureCode = null;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Analysis failed for {RecordId}", recordId);
            record.Status = EcgRecordStatus.Failed;
            record.FailureCode = "ANALYSIS_FAILED";
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
