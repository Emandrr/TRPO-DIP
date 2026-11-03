namespace SaEkg.Application.Medical;

public interface IMedicalDataService
{
    Task<MedicalCardDto?> GetMedicalCardAsync(Guid patientId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<MedicalCardDto> UpsertMedicalCardAsync(Guid patientId, UpsertMedicalCardRequest request, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task SoftDeleteMedicalCardAsync(Guid patientId, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiagnosisDto>> ListDiagnosesAsync(Guid patientId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<DiagnosisDto?> GetDiagnosisAsync(Guid id, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<DiagnosisDto> CreateDiagnosisAsync(CreateDiagnosisRequest request, Guid doctorId, CancellationToken cancellationToken = default);
    Task<DiagnosisDto?> UpdateDiagnosisAsync(Guid id, UpdateDiagnosisRequest request, Guid doctorId, CancellationToken cancellationToken = default);
    Task SoftDeleteDiagnosisAsync(Guid id, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecommendationDto>> ListRecommendationsAsync(Guid diagnosisId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<RecommendationDto?> GetRecommendationAsync(Guid id, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<RecommendationDto> CreateRecommendationAsync(Guid diagnosisId, UpsertRecommendationRequest request, Guid doctorId, CancellationToken cancellationToken = default);
    Task<RecommendationDto?> UpdateRecommendationAsync(Guid id, UpsertRecommendationRequest request, Guid doctorId, CancellationToken cancellationToken = default);
    Task SoftDeleteRecommendationAsync(Guid id, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrescriptionDto>> ListPrescriptionsAsync(Guid diagnosisId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<PrescriptionDto?> GetPrescriptionAsync(Guid id, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<PrescriptionDto> CreatePrescriptionAsync(Guid diagnosisId, UpsertPrescriptionRequest request, Guid doctorId, CancellationToken cancellationToken = default);
    Task<PrescriptionDto?> UpdatePrescriptionAsync(Guid id, UpsertPrescriptionRequest request, Guid doctorId, CancellationToken cancellationToken = default);
    Task SoftDeletePrescriptionAsync(Guid id, Guid actorId, string role, CancellationToken cancellationToken = default);
}
