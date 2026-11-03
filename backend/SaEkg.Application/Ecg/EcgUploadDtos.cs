namespace SaEkg.Application.Ecg;

public sealed record EcgUploadAcceptedResponse(Guid RecordId);

public sealed record EcgRecordDto(
    Guid Id,
    Guid PatientId,
    Guid DeviceId,
    DateTimeOffset UploadedAt,
    string OriginalFilename,
    long SampleCount,
    DateTime StartedAt,
    DateTime EndedAt,
    string Status,
    string? FailureCode,
    decimal UnusableRatio,
    bool RawPurged);

public sealed class EcgUploadException : Exception
{
    public EcgUploadException(string code, string message, object? details = null)
        : base(message)
    {
        Code = code;
        Details = details;
    }

    public string Code { get; }
    public object? Details { get; }
}
