namespace SaEkg.Domain.Enums;

public static class EcgRecordStatus
{
    public const string Uploaded = "Uploaded";
    public const string Validating = "Validating";
    public const string Queued = "Queued";
    public const string Processing = "Processing";
    public const string Done = "Done";
    public const string Failed = "Failed";
}
