namespace SaEkg.Api.Contracts;

public sealed class ApiError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public object? Details { get; init; }
    public string? RequestId { get; init; }
}

public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string ConsentRequired = "CONSENT_REQUIRED";
    public const string PasswordChangeRequired = "PASSWORD_CHANGE_REQUIRED";
    public const string InternalNetworkOnly = "INTERNAL_NETWORK_ONLY";
    public const string PatientSexRequired = "PATIENT_SEX_REQUIRED";
    public const string CsvCheckFailed = "CSV_CHECK_FAILED";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string InternalError = "INTERNAL_ERROR";
}
