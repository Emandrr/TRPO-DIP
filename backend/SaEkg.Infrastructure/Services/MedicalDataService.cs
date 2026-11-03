using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaEkg.Application.Ecg;
using SaEkg.Application.Medical;
using SaEkg.Domain.Entities;
using SaEkg.Domain.Enums;
using SaEkg.Infrastructure.Persistence;
using Serilog.Context;

namespace SaEkg.Infrastructure.Services;

public sealed class MedicalDataService(
    SaEkgDbContext db,
    ILogger<MedicalDataService> logger) : IMedicalDataService
{
    public async Task<MedicalCardDto?> GetMedicalCardAsync(Guid patientId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        await EnsurePatientAccessAsync(patientId, userId, role, readOnly: true, cancellationToken);
        var card = await db.MedicalCards.AsNoTracking().FirstOrDefaultAsync(x => x.PatientId == patientId, cancellationToken);
        if (card is null)
        {
            return null;
        }

        await AuditAsync(userId, "read", "medical_card", card.Id, cancellationToken);
        return MapCard(card);
    }

    public async Task<MedicalCardDto> UpsertMedicalCardAsync(
        Guid patientId,
        UpsertMedicalCardRequest request,
        Guid actorId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (role != UserRole.Doctor)
        {
            throw new EcgUploadException("FORBIDDEN", "Медкарту может создавать/изменять только врач.");
        }

        await EnsurePatientAccessAsync(patientId, actorId, role, readOnly: false, cancellationToken);
        if (!await db.Patients.AnyAsync(x => x.UserId == patientId, cancellationToken))
        {
            throw new EcgUploadException("NOT_FOUND", "Пациент не найден.");
        }

        ValidateCard(request);

        var card = await db.MedicalCards.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.PatientId == patientId, cancellationToken);
        if (card is null)
        {
            card = new MedicalCard { Id = Guid.NewGuid(), PatientId = patientId };
            db.MedicalCards.Add(card);
        }
        else if (card.IsDeleted)
        {
            card.IsDeleted = false;
            card.DeletedAt = null;
            card.DeletedBy = null;
        }

        MedDataValidators.TryParseRuDate(request.BirthDate, out var birth);
        card.LastName = request.LastName.Trim();
        card.FirstName = request.FirstName.Trim();
        card.MiddleName = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim();
        card.BirthDate = birth;
        card.Sex = MedDataValidators.MapSexToDb(request.Sex);
        await db.SaveChangesAsync(cancellationToken);
        return MapCard(card);
    }

    public async Task SoftDeleteMedicalCardAsync(Guid patientId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        if (role is not (UserRole.Doctor or UserRole.Admin))
        {
            throw new EcgUploadException("FORBIDDEN", "Недостаточно прав.");
        }

        if (role == UserRole.Doctor)
        {
            await EnsurePatientAccessAsync(patientId, actorId, role, readOnly: false, cancellationToken);
        }

        var card = await db.MedicalCards.FirstOrDefaultAsync(x => x.PatientId == patientId, cancellationToken)
            ?? throw new EcgUploadException("NOT_FOUND", "Медкарта не найдена.");
        card.IsDeleted = true;
        card.DeletedAt = DateTimeOffset.UtcNow;
        card.DeletedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DiagnosisDto>> ListDiagnosesAsync(Guid patientId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        await EnsurePatientAccessAsync(patientId, userId, role, readOnly: true, cancellationToken);
        var items = await db.Diagnoses.AsNoTracking()
            .Include(x => x.AnomalyLinks)
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.DiagnosedOn)
            .ToListAsync(cancellationToken);
        if (items.Count > 0)
        {
            foreach (var d in items)
            {
                db.AuditAccesses.Add(new AuditAccess
                {
                    Id = Guid.NewGuid(),
                    Ts = DateTimeOffset.UtcNow,
                    UserId = userId,
                    Action = "read",
                    Entity = "diagnosis",
                    EntityId = d.Id
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return items.Select(MapDiagnosis).ToList();
    }

    public async Task<DiagnosisDto?> GetDiagnosisAsync(Guid id, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var d = await db.Diagnoses.AsNoTracking().Include(x => x.AnomalyLinks).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (d is null)
        {
            return null;
        }

        await EnsurePatientAccessAsync(d.PatientId, userId, role, readOnly: true, cancellationToken);
        await AuditAsync(userId, "read", "diagnosis", d.Id, cancellationToken);
        return MapDiagnosis(d);
    }

    public async Task<DiagnosisDto> CreateDiagnosisAsync(CreateDiagnosisRequest request, Guid doctorId, CancellationToken cancellationToken = default)
    {
        await EnsurePatientAccessAsync(request.PatientId, doctorId, UserRole.Doctor, readOnly: false, cancellationToken);
        ValidateDiagnosis(request.Title, request.Description, request.DiagnosedOn);
        MedDataValidators.TryParseRuDate(request.DiagnosedOn, out var diagnosedOn);
        await ValidateRecordAndReportAsync(request.PatientId, request.RecordId, request.ReportId, cancellationToken);
        await ValidateAnomaliesAsync(request.PatientId, request.ReportId, request.AnomalyIds, cancellationToken);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var entity = new Diagnosis
        {
            Id = Guid.NewGuid(),
            PatientId = request.PatientId,
            DoctorId = doctorId,
            RecordId = request.RecordId,
            ReportId = request.ReportId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description,
            DiagnosedOn = diagnosedOn,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Diagnoses.Add(entity);
        foreach (var anomalyId in request.AnomalyIds ?? Array.Empty<Guid>())
        {
            db.DiagnosisAnomalies.Add(new DiagnosisAnomaly { DiagnosisId = entity.Id, AnomalyId = anomalyId });
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (await GetDiagnosisAsync(entity.Id, doctorId, UserRole.Doctor, cancellationToken))!;
    }

    public async Task<DiagnosisDto?> UpdateDiagnosisAsync(Guid id, UpdateDiagnosisRequest request, Guid doctorId, CancellationToken cancellationToken = default)
    {
        var entity = await db.Diagnoses.Include(x => x.AnomalyLinks).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        await EnsurePatientAccessAsync(entity.PatientId, doctorId, UserRole.Doctor, readOnly: false, cancellationToken);
        ValidateDiagnosis(request.Title, request.Description, request.DiagnosedOn);
        MedDataValidators.TryParseRuDate(request.DiagnosedOn, out var diagnosedOn);
        await ValidateAnomaliesAsync(entity.PatientId, entity.ReportId, request.AnomalyIds, cancellationToken);

        entity.Title = request.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description;
        entity.DiagnosedOn = diagnosedOn;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        db.DiagnosisAnomalies.RemoveRange(entity.AnomalyLinks);
        foreach (var anomalyId in request.AnomalyIds ?? Array.Empty<Guid>())
        {
            db.DiagnosisAnomalies.Add(new DiagnosisAnomaly { DiagnosisId = entity.Id, AnomalyId = anomalyId });
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetDiagnosisAsync(entity.Id, doctorId, UserRole.Doctor, cancellationToken);
    }

    public async Task SoftDeleteDiagnosisAsync(Guid id, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var entity = await db.Diagnoses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new EcgUploadException("NOT_FOUND", "Диагноз не найден.");
        if (role == UserRole.Doctor)
        {
            await EnsurePatientAccessAsync(entity.PatientId, actorId, role, readOnly: false, cancellationToken);
        }
        else if (role != UserRole.Admin)
        {
            throw new EcgUploadException("FORBIDDEN", "Недостаточно прав.");
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.DeletedBy = actorId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecommendationDto>> ListRecommendationsAsync(Guid diagnosisId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var diagnosis = await RequireDiagnosisAccessAsync(diagnosisId, userId, role, cancellationToken);
        var items = await db.Recommendations.AsNoTracking()
            .Where(x => x.DiagnosisId == diagnosis.Id)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return items.Select(MapRecommendation).ToList();
    }

    public async Task<RecommendationDto?> GetRecommendationAsync(Guid id, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var entity = await db.Recommendations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        await RequireDiagnosisAccessAsync(entity.DiagnosisId, userId, role, cancellationToken);
        return MapRecommendation(entity);
    }

    public async Task<RecommendationDto> CreateRecommendationAsync(Guid diagnosisId, UpsertRecommendationRequest request, Guid doctorId, CancellationToken cancellationToken = default)
    {
        var diagnosis = await RequireDiagnosisAccessAsync(diagnosisId, doctorId, UserRole.Doctor, cancellationToken);
        ValidateRecommendation(request);
        var entity = new Recommendation
        {
            Id = Guid.NewGuid(),
            DiagnosisId = diagnosis.Id,
            Text = request.Text.Trim(),
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Recommendations.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return MapRecommendation(entity);
    }

    public async Task<RecommendationDto?> UpdateRecommendationAsync(Guid id, UpsertRecommendationRequest request, Guid doctorId, CancellationToken cancellationToken = default)
    {
        var entity = await db.Recommendations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        await RequireDiagnosisAccessAsync(entity.DiagnosisId, doctorId, UserRole.Doctor, cancellationToken);
        ValidateRecommendation(request);
        entity.Text = request.Text.Trim();
        entity.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return MapRecommendation(entity);
    }

    public async Task SoftDeleteRecommendationAsync(Guid id, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var entity = await db.Recommendations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new EcgUploadException("NOT_FOUND", "Рекомендация не найдена.");
        var diagnosis = await db.Diagnoses.AsNoTracking().FirstAsync(x => x.Id == entity.DiagnosisId, cancellationToken);
        if (role == UserRole.Doctor)
        {
            await EnsurePatientAccessAsync(diagnosis.PatientId, actorId, role, readOnly: false, cancellationToken);
        }
        else if (role != UserRole.Admin)
        {
            throw new EcgUploadException("FORBIDDEN", "Недостаточно прав.");
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.DeletedBy = actorId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PrescriptionDto>> ListPrescriptionsAsync(Guid diagnosisId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var diagnosis = await RequireDiagnosisAccessAsync(diagnosisId, userId, role, cancellationToken);
        var items = await db.Prescriptions.AsNoTracking()
            .Where(x => x.DiagnosisId == diagnosis.Id)
            .OrderBy(x => x.StartDate)
            .ToListAsync(cancellationToken);
        return items.Select(MapPrescription).ToList();
    }

    public async Task<PrescriptionDto?> GetPrescriptionAsync(Guid id, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var entity = await db.Prescriptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        await RequireDiagnosisAccessAsync(entity.DiagnosisId, userId, role, cancellationToken);
        return MapPrescription(entity);
    }

    public async Task<PrescriptionDto> CreatePrescriptionAsync(Guid diagnosisId, UpsertPrescriptionRequest request, Guid doctorId, CancellationToken cancellationToken = default)
    {
        var diagnosis = await RequireDiagnosisAccessAsync(diagnosisId, doctorId, UserRole.Doctor, cancellationToken);
        ValidatePrescription(request);
        MedDataValidators.TryParseRuDate(request.StartDate, out var start);
        MedDataValidators.TryParseRuDate(request.EndDate, out var end);
        var entity = new Prescription
        {
            Id = Guid.NewGuid(),
            DiagnosisId = diagnosis.Id,
            DrugName = request.DrugName.Trim(),
            StartDate = start,
            EndDate = end,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Prescriptions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return MapPrescription(entity);
    }

    public async Task<PrescriptionDto?> UpdatePrescriptionAsync(Guid id, UpsertPrescriptionRequest request, Guid doctorId, CancellationToken cancellationToken = default)
    {
        var entity = await db.Prescriptions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        await RequireDiagnosisAccessAsync(entity.DiagnosisId, doctorId, UserRole.Doctor, cancellationToken);
        ValidatePrescription(request);
        MedDataValidators.TryParseRuDate(request.StartDate, out var start);
        MedDataValidators.TryParseRuDate(request.EndDate, out var end);
        entity.DrugName = request.DrugName.Trim();
        entity.StartDate = start;
        entity.EndDate = end;
        entity.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return MapPrescription(entity);
    }

    public async Task SoftDeletePrescriptionAsync(Guid id, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var entity = await db.Prescriptions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new EcgUploadException("NOT_FOUND", "Назначение не найдено.");
        var diagnosis = await db.Diagnoses.AsNoTracking().FirstAsync(x => x.Id == entity.DiagnosisId, cancellationToken);
        if (role == UserRole.Doctor)
        {
            await EnsurePatientAccessAsync(diagnosis.PatientId, actorId, role, readOnly: false, cancellationToken);
        }
        else if (role != UserRole.Admin)
        {
            throw new EcgUploadException("FORBIDDEN", "Недостаточно прав.");
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.DeletedBy = actorId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsurePatientAccessAsync(Guid patientId, Guid userId, string role, bool readOnly, CancellationToken cancellationToken)
    {
        if (role == UserRole.Admin)
        {
            return;
        }

        if (role == UserRole.Patient)
        {
            if (!readOnly || patientId != userId)
            {
                throw new EcgUploadException(patientId == userId ? "FORBIDDEN" : "NOT_FOUND",
                    patientId == userId ? "Недостаточно прав." : "Не найдено.");
            }

            return;
        }

        if (role == UserRole.Doctor)
        {
            var assigned = await db.DoctorPatients.AnyAsync(x => x.DoctorId == userId && x.PatientId == patientId, cancellationToken);
            if (!assigned)
            {
                throw new EcgUploadException("FORBIDDEN", "Пациент не закреплён за врачом.");
            }

            return;
        }

        throw new EcgUploadException("FORBIDDEN", "Недостаточно прав.");
    }

    private async Task<Diagnosis> RequireDiagnosisAccessAsync(Guid diagnosisId, Guid userId, string role, CancellationToken cancellationToken)
    {
        var diagnosis = await db.Diagnoses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == diagnosisId, cancellationToken)
            ?? throw new EcgUploadException("NOT_FOUND", "Диагноз не найден.");
        await EnsurePatientAccessAsync(diagnosis.PatientId, userId, role, readOnly: role != UserRole.Doctor, cancellationToken);
        return diagnosis;
    }

    private async Task ValidateRecordAndReportAsync(Guid patientId, Guid? recordId, Guid? reportId, CancellationToken cancellationToken)
    {
        if (recordId is not null)
        {
            var recordOk = await db.EcgRecords.AsNoTracking()
                .AnyAsync(x => x.Id == recordId && x.PatientId == patientId, cancellationToken);
            if (!recordOk)
            {
                throw new MedValidationException(["recordId: запись не принадлежит пациенту."]);
            }
        }

        if (reportId is not null)
        {
            var report = await db.AnalysisReports.AsNoTracking()
                .Include(x => x.Record)
                .FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken);
            if (report is null || report.Record.PatientId != patientId)
            {
                throw new MedValidationException(["reportId: отчёт не принадлежит пациенту."]);
            }

            if (recordId is not null && report.RecordId != recordId)
            {
                throw new MedValidationException(["reportId: отчёт не относится к указанной записи."]);
            }
        }
    }

    private async Task ValidateAnomaliesAsync(Guid patientId, Guid? reportId, IReadOnlyList<Guid>? anomalyIds, CancellationToken cancellationToken)
    {
        if (anomalyIds is null || anomalyIds.Count == 0)
        {
            return;
        }

        if (reportId is null)
        {
            throw new MedValidationException(["anomalyIds: требуется reportId для привязки аномалий."]);
        }

        var report = await db.AnalysisReports.AsNoTracking()
            .Include(x => x.Record)
            .FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken);
        if (report is null || report.Record.PatientId != patientId)
        {
            throw new MedValidationException(["reportId: отчёт не принадлежит пациенту."]);
        }

        var valid = await db.Anomalies.AsNoTracking()
            .Where(a => a.ReportId == reportId && anomalyIds.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);
        if (valid.Count != anomalyIds.Distinct().Count())
        {
            throw new MedValidationException(["anomalyIds: есть аномалии вне отчёта пациента."]);
        }
    }

    private async Task AuditAsync(Guid userId, string action, string entity, Guid entityId, CancellationToken cancellationToken)
    {
        db.AuditAccesses.Add(new AuditAccess
        {
            Id = Guid.NewGuid(),
            Ts = DateTimeOffset.UtcNow,
            UserId = userId,
            Action = action,
            Entity = entity,
            EntityId = entityId
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private void ValidateCard(UpsertMedicalCardRequest request)
    {
        var errors = MedDataValidators.Collect(
            MedDataValidators.ValidateRequiredLine(request.LastName, "lastName"),
            MedDataValidators.ValidateRequiredLine(request.FirstName, "firstName"),
            string.IsNullOrWhiteSpace(request.MiddleName) ? null : MedDataValidators.ValidateRequiredLine(request.MiddleName, "middleName"),
            MedDataValidators.ValidateBirthDate(request.BirthDate),
            MedDataValidators.ValidateSexRu(request.Sex));
        ThrowIfInvalid(errors);
    }

    private void ValidateDiagnosis(string title, string? description, string diagnosedOn)
    {
        var errors = MedDataValidators.Collect(
            MedDataValidators.ValidateRequiredLine(title, "title"),
            MedDataValidators.ValidateOptionalMultiline(description, "description"),
            MedDataValidators.ValidateDiagnosisDate(diagnosedOn));
        ThrowIfInvalid(errors);
    }

    private void ValidateRecommendation(UpsertRecommendationRequest request)
    {
        var errors = MedDataValidators.Collect(
            string.IsNullOrWhiteSpace(request.Text) || request.Text.Trim().Length == 0 || request.Text.Length > 5000
                ? "text: обязательное поле, не более 5000 символов."
                : null,
            MedDataValidators.ValidateOptionalMultiline(request.Comment, "comment"));
        ThrowIfInvalid(errors);
    }

    private void ValidatePrescription(UpsertPrescriptionRequest request)
    {
        var errors = MedDataValidators.Collect(
            MedDataValidators.ValidateRequiredLine(request.DrugName, "drugName"),
            MedDataValidators.ValidatePrescriptionDates(request.StartDate, request.EndDate),
            MedDataValidators.ValidateOptionalMultiline(request.Comment, "comment"));
        ThrowIfInvalid(errors);
    }

    private void ThrowIfInvalid(List<string> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        using (LogContext.PushProperty("Module", "MedValidation"))
        using (LogContext.PushProperty("Code", "VALIDATION_ERROR"))
        {
            logger.LogWarning("Medical validation failed: {Errors}", string.Join("; ", errors));
        }

        throw new MedValidationException(errors);
    }

    private static MedicalCardDto MapCard(MedicalCard c) => new(
        c.Id,
        c.PatientId,
        c.LastName,
        c.FirstName,
        c.MiddleName,
        c.BirthDate.ToString("dd.MM.yyyy"),
        MedDataValidators.MapSexToRu(c.Sex));

    private static DiagnosisDto MapDiagnosis(Diagnosis d) => new(
        d.Id,
        d.PatientId,
        d.DoctorId,
        d.RecordId,
        d.ReportId,
        d.Title,
        d.Description,
        d.DiagnosedOn.ToString("dd.MM.yyyy"),
        d.AnomalyLinks.Select(x => x.AnomalyId).ToList());

    private static RecommendationDto MapRecommendation(Recommendation r) =>
        new(r.Id, r.DiagnosisId, r.Text, r.Comment);

    private static PrescriptionDto MapPrescription(Prescription p) => new(
        p.Id,
        p.DiagnosisId,
        p.DrugName,
        p.StartDate.ToString("dd.MM.yyyy"),
        p.EndDate.ToString("dd.MM.yyyy"),
        p.Comment);
}
