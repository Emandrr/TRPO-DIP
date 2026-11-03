namespace SaEkg.Application.Ecg;

public interface IEcgUploadService
{
    Task<EcgUploadAcceptedResponse> UploadAsync(
        Guid patientId,
        Guid deviceId,
        string fileName,
        Stream content,
        long? contentLength,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EcgRecordDto>> ListForUserAsync(
        Guid userId,
        string role,
        string? status = null,
        DateTimeOffset? uploadedFrom = null,
        DateTimeOffset? uploadedTo = null,
        CancellationToken cancellationToken = default);

    Task<EcgRecordDto?> GetForUserAsync(
        Guid recordId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default);
}
