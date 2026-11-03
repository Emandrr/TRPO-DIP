namespace SaEkg.Domain.Constants;

public static class CsvValidationConstants
{
    public const long MaxFileBytes = 1L * 1024 * 1024 * 1024;
    public const int SampleIntervalMs = 4;
    public const int MinValue = -5000;
    public const int MaxValue = 5000;
    public const double MaxUnusableRatio = 0.30;
    public const int MinDurationMs = 10_000;
    public const int MaxDurationMs = 24 * 60 * 60 * 1000;
    public const string Extension = ".csv";
    public const string ErrorValue = "Error";
    public const string NoErrorValue = "NoError";
}
