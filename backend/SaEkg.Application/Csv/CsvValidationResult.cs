namespace SaEkg.Application.Csv;

public sealed class CsvValidationResult
{
    public bool Ok { get; init; }
    public int? FailedCheck { get; init; }
    public int? Line { get; init; }
    public string? Message { get; init; }
    public long SampleCount { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public decimal UnusableRatio { get; init; }
    public IReadOnlyList<CsvGapInfo> Gaps { get; init; } = Array.Empty<CsvGapInfo>();

    public static CsvValidationResult Success(
        long sampleCount,
        DateTime startedAt,
        DateTime endedAt,
        decimal unusableRatio,
        IReadOnlyList<CsvGapInfo> gaps) => new()
    {
        Ok = true,
        SampleCount = sampleCount,
        StartedAt = startedAt,
        EndedAt = endedAt,
        UnusableRatio = unusableRatio,
        Gaps = gaps
    };

    public static CsvValidationResult Fail(int check, string message, int? line = null) => new()
    {
        Ok = false,
        FailedCheck = check,
        Line = line,
        Message = message
    };
}

public sealed record CsvGapInfo(long StartOffsetMs, long EndOffsetMs, string Reason);
