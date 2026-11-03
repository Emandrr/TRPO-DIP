using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SaEkg.Application.Analysis;
using SaEkg.Application.Ecg;
using SaEkg.Domain.Enums;
using SaEkg.Infrastructure.Persistence;

namespace SaEkg.Infrastructure.Services;

public sealed class EcgReportService(
    SaEkgDbContext db,
    IEcgSignalStore signalStore) : IEcgReportService
{
    public async Task<EcgReportDto?> GetReportAsync(Guid recordId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var record = await db.EcgRecords.AsNoTracking()
            .Include(x => x.Gaps)
            .Include(x => x.AnalysisReport)!.ThenInclude(r => r!.Anomalies)
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken);
        if (record?.AnalysisReport is null)
        {
            return null;
        }

        if (!await CanAccessAsync(record.PatientId, record.AnalysisReport.Status, userId, role, cancellationToken))
        {
            return role == UserRole.Doctor
                ? throw new EcgUploadException("FORBIDDEN", "Нет доступа к отчёту.")
                : null;
        }

        AnalysisSummary? summary = null;
        try
        {
            summary = JsonSerializer.Deserialize<AnalysisSummary>(record.AnalysisReport.SummaryJson);
        }
        catch
        {
        }

        var durationSec = (record.EndedAt - record.StartedAt).TotalSeconds + 0.004;
        return new EcgReportDto(
            record.AnalysisReport.Id,
            record.Id,
            "II",
            record.AnalysisReport.Status,
            record.StartedAt,
            record.EndedAt,
            durationSec,
            (double)record.UnusableRatio * 100.0,
            new AnalysisSummaryDto(
                summary?.HrMean,
                summary?.HrMin,
                summary?.HrMax,
                summary?.RrMean,
                summary?.QtcMean,
                summary?.RAmpMean,
                summary?.CyclesTotal ?? 0,
                summary?.CyclesUndetermined ?? 0),
            record.Gaps.Select(g => new EcgGapDto(g.StartOffsetMs, g.EndOffsetMs, g.Reason)).ToList(),
            record.AnalysisReport.Anomalies.Select(a => new EcgAnomalyDto(
                a.Id,
                a.IndicatorCode,
                Title(a.IndicatorCode, a.Direction),
                a.StartOffsetMs,
                a.EndOffsetMs,
                a.ValueMin is null ? null : (double)a.ValueMin.Value,
                a.ValueMax is null ? null : (double)a.ValueMax.Value,
                a.Unit,
                a.ReferenceText)).ToList());
    }

    public async Task AcceptAsync(Guid recordId, Guid doctorId, string? comment, CancellationToken cancellationToken = default)
        => await DecideAsync(recordId, doctorId, ReportStatus.Accepted, comment, cancellationToken);

    public async Task RejectAsync(Guid recordId, Guid doctorId, string? comment, CancellationToken cancellationToken = default)
        => await DecideAsync(recordId, doctorId, ReportStatus.Rejected, comment, cancellationToken);

    public async Task<IReadOnlyList<DownsampledPoint>> GetSignalAsync(
        Guid recordId,
        Guid userId,
        string role,
        long fromMs,
        long toMs,
        int maxPoints,
        CancellationToken cancellationToken = default)
    {
        var record = await db.EcgRecords.AsNoTracking()
            .Include(x => x.AnalysisReport)
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken);
        if (record is null)
        {
            return Array.Empty<DownsampledPoint>();
        }

        var status = record.AnalysisReport?.Status ?? ReportStatus.Pending;
        if (!await CanAccessAsync(record.PatientId, status, userId, role, cancellationToken))
        {
            if (role == UserRole.Doctor)
            {
                throw new EcgUploadException("FORBIDDEN", "Нет доступа к сигналу.");
            }

            return Array.Empty<DownsampledPoint>();
        }

        maxPoints = Math.Clamp(maxPoints, 1, 5000);
        return await signalStore.ReadFilteredDownsampledAsync(recordId, fromMs, toMs, maxPoints, cancellationToken);
    }

    private async Task DecideAsync(Guid recordId, Guid doctorId, string status, string? comment, CancellationToken cancellationToken)
    {
        var record = await db.EcgRecords.Include(x => x.AnalysisReport)
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken)
            ?? throw new EcgUploadException("NOT_FOUND", "Запись не найдена.");

        var assigned = await db.DoctorPatients.AnyAsync(
            x => x.DoctorId == doctorId && x.PatientId == record.PatientId,
            cancellationToken);
        if (!assigned)
        {
            throw new EcgUploadException("FORBIDDEN", "Пациент не закреплён за врачом.");
        }

        if (record.AnalysisReport is null)
        {
            throw new EcgUploadException("NOT_FOUND", "Отчёт ещё не готов.");
        }

        record.AnalysisReport.Status = status;
        record.AnalysisReport.DecidedBy = doctorId;
        record.AnalysisReport.DecidedAt = DateTimeOffset.UtcNow;
        record.AnalysisReport.DecisionComment = comment;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> CanAccessAsync(Guid patientId, string reportStatus, Guid userId, string role, CancellationToken cancellationToken)
    {
        if (role == UserRole.Admin)
        {
            return true;
        }

        if (role == UserRole.Patient)
        {
            return patientId == userId && reportStatus == ReportStatus.Accepted;
        }

        if (role == UserRole.Doctor)
        {
            return await db.DoctorPatients.AnyAsync(x => x.DoctorId == userId && x.PatientId == patientId, cancellationToken);
        }

        return false;
    }

    private static string Title(string code, string direction) => (code, direction) switch
    {
        ("HR", AnomalyDirection.High) => "Тахикардия",
        ("HR", AnomalyDirection.Low) => "Брадикардия",
        ("PAUSE", _) => "Пауза",
        ("RR", _) => "Нарушение RR",
        ("QTC", _) => "Удлинение QTc",
        ("ST", _) => "Смещение ST",
        _ => code
    };
}
