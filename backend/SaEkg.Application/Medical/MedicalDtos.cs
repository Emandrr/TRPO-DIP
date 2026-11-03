namespace SaEkg.Application.Medical;

public sealed record MedicalCardDto(
    Guid Id,
    Guid PatientId,
    string? LastName,
    string? FirstName,
    string? MiddleName,
    string BirthDate,
    string Sex);

public sealed record UpsertMedicalCardRequest(
    string LastName,
    string FirstName,
    string? MiddleName,
    string BirthDate,
    string Sex);

public sealed record DiagnosisDto(
    Guid Id,
    Guid PatientId,
    Guid DoctorId,
    Guid? RecordId,
    Guid? ReportId,
    string Title,
    string? Description,
    string DiagnosedOn,
    IReadOnlyList<Guid> AnomalyIds);

public sealed record CreateDiagnosisRequest(
    Guid PatientId,
    string Title,
    string? Description,
    string DiagnosedOn,
    Guid? RecordId,
    Guid? ReportId,
    IReadOnlyList<Guid>? AnomalyIds);

public sealed record UpdateDiagnosisRequest(
    string Title,
    string? Description,
    string DiagnosedOn,
    IReadOnlyList<Guid>? AnomalyIds);

public sealed record RecommendationDto(Guid Id, Guid DiagnosisId, string Text, string? Comment);

public sealed record UpsertRecommendationRequest(string Text, string? Comment);

public sealed record PrescriptionDto(
    Guid Id,
    Guid DiagnosisId,
    string DrugName,
    string StartDate,
    string EndDate,
    string? Comment);

public sealed record UpsertPrescriptionRequest(
    string DrugName,
    string StartDate,
    string EndDate,
    string? Comment);
