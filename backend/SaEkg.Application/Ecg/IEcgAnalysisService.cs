namespace SaEkg.Application.Ecg;

public interface IEcgAnalysisService
{
    Task AnalyzeRecordAsync(Guid recordId, CancellationToken cancellationToken = default);
    Task EnqueueAsync(Guid recordId, CancellationToken cancellationToken = default);
    Task ReanalyzeAsync(Guid recordId, CancellationToken cancellationToken = default);
}

public interface IEcgReportService
{
    Task<EcgReportDto?> GetReportAsync(Guid recordId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task AcceptAsync(Guid recordId, Guid doctorId, string? comment, CancellationToken cancellationToken = default);
    Task RejectAsync(Guid recordId, Guid doctorId, string? comment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DownsampledPoint>> GetSignalAsync(
        Guid recordId,
        Guid userId,
        string role,
        long fromMs,
        long toMs,
        int maxPoints,
        CancellationToken cancellationToken = default);
}

public sealed record EcgReportDto(
    Guid Id,
    Guid RecordId,
    string Lead,
    string Status,
    DateTime StartedAt,
    DateTime EndedAt,
    double DurationSec,
    double UnusablePercent,
    AnalysisSummaryDto Summary,
    IReadOnlyList<EcgGapDto> Gaps,
    IReadOnlyList<EcgAnomalyDto> Anomalies);

public sealed record AnalysisSummaryDto(
    double? HrMean,
    double? HrMin,
    double? HrMax,
    double? RrMean,
    double? QtcMean,
    double? RAmpMean,
    int CyclesTotal,
    int CyclesUndetermined);

public sealed record EcgGapDto(long StartMs, long EndMs, string Reason);

public sealed record EcgAnomalyDto(
    Guid Id,
    string Code,
    string Title,
    long StartMs,
    long EndMs,
    double? ValueMin,
    double? ValueMax,
    string? Unit,
    string? Reference);
