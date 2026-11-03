using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaEkg.Application.Csv;
using SaEkg.Application.Ecg;
using SaEkg.Domain.Constants;
using SaEkg.Domain.Entities;
using SaEkg.Domain.Enums;
using SaEkg.Infrastructure.Persistence;
using Serilog.Context;

namespace SaEkg.Infrastructure.Services;

public sealed class EcgUploadService(
    SaEkgDbContext db,
    IEcgSignalStore signalStore,
    IEcgAnalysisService analysisService,
    ILogger<EcgUploadService> logger) : IEcgUploadService
{
    private readonly CsvValidator _validator = new();

    public async Task<EcgUploadAcceptedResponse> UploadAsync(
        Guid patientId,
        Guid deviceId,
        string fileName,
        Stream content,
        long? contentLength,
        CancellationToken cancellationToken = default)
    {
        var patient = await db.Patients.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == patientId, cancellationToken);
        if (patient is null)
        {
            throw new EcgUploadException("FORBIDDEN", "Только пациент может загружать ЭКГ.");
        }

        if (!patient.ConsentSigned)
        {
            throw new EcgUploadException("CONSENT_REQUIRED", "Требуется согласие на обработку персональных данных.");
        }

        var deviceExists = await db.Devices.AnyAsync(x => x.Id == deviceId, cancellationToken);
        if (!deviceExists)
        {
            throw new EcgUploadException("VALIDATION_ERROR", "Устройство не найдено.", new { deviceId });
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"saekg-upload-{Guid.NewGuid():N}.csv");
        try
        {
            await using (var temp = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                1024 * 64,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await CopyWithLimitAsync(content, temp, contentLength, cancellationToken);
                temp.Position = 0;
                var validation = _validator.Validate(temp, fileName, contentLength ?? temp.Length);
                if (!validation.Ok)
                {
                    using (LogContext.PushProperty("Module", "CsvValidation"))
                    using (LogContext.PushProperty("Code", $"CSV_CHECK_{validation.FailedCheck}"))
                    {
                        logger.LogWarning(
                            "CSV validation failed. Check={Check} Line={Line}",
                            validation.FailedCheck,
                            validation.Line);
                    }

                    throw new EcgUploadException(
                        "CSV_CHECK_FAILED",
                        validation.Message ?? "Файл CSV не прошёл проверку.",
                        new { check = validation.FailedCheck, line = validation.Line });
                }

                var recordId = Guid.NewGuid();
                var now = DateTimeOffset.UtcNow;
                var record = new EcgRecord
                {
                    Id = recordId,
                    PatientId = patientId,
                    DeviceId = deviceId,
                    UploadedAt = now,
                    OriginalFilename = Path.GetFileName(fileName),
                    SampleCount = validation.SampleCount,
                    StartedAt = validation.StartedAt!.Value,
                    EndedAt = validation.EndedAt!.Value,
                    Status = EcgRecordStatus.Validating,
                    RawPurgeAt = now.AddDays(30),
                    UnusableRatio = validation.UnusableRatio
                };

                db.EcgRecords.Add(record);
                foreach (var gap in validation.Gaps)
                {
                    db.EcgGaps.Add(new EcgGap
                    {
                        Id = Guid.NewGuid(),
                        RecordId = recordId,
                        StartOffsetMs = gap.StartOffsetMs,
                        EndOffsetMs = gap.EndOffsetMs,
                        Reason = gap.Reason
                    });
                }

                await db.SaveChangesAsync(cancellationToken);

                try
                {
                    temp.Position = 0;
                    await signalStore.EnsureSchemaAsync(cancellationToken);
                    await foreach (var batch in CsvSampleReader.ReadRawBatchesAsync(temp, cancellationToken: cancellationToken))
                    {
                        await signalStore.AppendRawSamplesAsync(recordId, batch, cancellationToken);
                    }

                    record.Status = EcgRecordStatus.Queued;
                    await db.SaveChangesAsync(cancellationToken);
                    await analysisService.EnqueueAsync(recordId, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to persist ECG signal for {RecordId}", recordId);
                    await signalStore.DeleteByRecordIdAsync(recordId, CancellationToken.None);
                    record.Status = EcgRecordStatus.Failed;
                    record.FailureCode = "SIGNAL_STORE_FAILED";
                    await db.SaveChangesAsync(CancellationToken.None);
                    throw new EcgUploadException("INTERNAL_ERROR", "Не удалось сохранить сигнал ЭКГ.");
                }

                return new EcgUploadAcceptedResponse(recordId);
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }
    }

    public async Task<IReadOnlyList<EcgRecordDto>> ListForUserAsync(
        Guid userId,
        string role,
        string? status = null,
        DateTimeOffset? uploadedFrom = null,
        DateTimeOffset? uploadedTo = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<EcgRecord> q = db.EcgRecords.AsNoTracking();
        if (role == UserRole.Patient)
        {
            q = q.Where(x => x.PatientId == userId
                && x.AnalysisReport != null
                && x.AnalysisReport.Status == ReportStatus.Accepted);
        }
        else if (role == UserRole.Doctor)
        {
            q = q.Where(x => db.DoctorPatients.Any(l => l.DoctorId == userId && l.PatientId == x.PatientId));
        }
        else if (role != UserRole.Admin)
        {
            return Array.Empty<EcgRecordDto>();
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            q = q.Where(x => x.Status == status);
        }

        if (uploadedFrom is not null)
        {
            q = q.Where(x => x.UploadedAt >= uploadedFrom);
        }

        if (uploadedTo is not null)
        {
            q = q.Where(x => x.UploadedAt <= uploadedTo);
        }

        return await q.OrderByDescending(x => x.UploadedAt)
            .Select(x => new EcgRecordDto(
                x.Id,
                x.PatientId,
                x.DeviceId,
                x.UploadedAt,
                x.OriginalFilename,
                x.SampleCount,
                x.StartedAt,
                x.EndedAt,
                x.Status,
                x.FailureCode,
                x.UnusableRatio,
                x.RawPurged))
            .ToListAsync(cancellationToken);
    }

    public async Task<EcgRecordDto?> GetForUserAsync(
        Guid recordId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var record = await db.EcgRecords.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken);
        if (record is null)
        {
            return null;
        }

        if (role == UserRole.Patient)
        {
            if (record.PatientId != userId)
            {
                return null;
            }
        }
        else if (role == UserRole.Doctor)
        {
            var assigned = await db.DoctorPatients.AnyAsync(
                x => x.DoctorId == userId && x.PatientId == record.PatientId,
                cancellationToken);
            if (!assigned)
            {
                throw new EcgUploadException("FORBIDDEN", "Нет доступа к записи пациента.");
            }
        }
        else if (role != UserRole.Admin)
        {
            return null;
        }

        return new EcgRecordDto(
            record.Id,
            record.PatientId,
            record.DeviceId,
            record.UploadedAt,
            record.OriginalFilename,
            record.SampleCount,
            record.StartedAt,
            record.EndedAt,
            record.Status,
            record.FailureCode,
            record.UnusableRatio,
            record.RawPurged);
    }

    private static async Task CopyWithLimitAsync(
        Stream source,
        Stream destination,
        long? contentLength,
        CancellationToken cancellationToken)
    {
        if (contentLength is > CsvValidationConstants.MaxFileBytes)
        {
            throw new EcgUploadException(
                "CSV_CHECK_FAILED",
                "Размер файла превышает 1 ГБ.",
                new { check = 1, line = (int?)null });
        }

        var buffer = new byte[80 * 1024];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > CsvValidationConstants.MaxFileBytes)
            {
                throw new EcgUploadException(
                    "CSV_CHECK_FAILED",
                    "Размер файла превышает 1 ГБ.",
                    new { check = 1, line = (int?)null });
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }
}
