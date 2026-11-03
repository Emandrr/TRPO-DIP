namespace SaEkg.Application.Analysis;

public sealed record SignalSegment(long StartOffsetMs, long EndOffsetMs, float[] ValuesMv);

public sealed record FiducialPoints(
    long RMs,
    long? QOnMs,
    long? SOffMs,
    long? POnMs,
    long? POffMs,
    long? TOffMs,
    long? JMs,
    float BaselineMv,
    float RAmpMv);

public sealed record DetectedAnomaly(
    string IndicatorCode,
    string Direction,
    string Title,
    long StartOffsetMs,
    long EndOffsetMs,
    double? ValueMin,
    double? ValueMax,
    string Unit,
    string ReferenceText,
    int CycleCount);

public sealed record AnalysisSummary(
    double? HrMean,
    double? HrMin,
    double? HrMax,
    double? RrMean,
    double? QtcMean,
    double? RAmpMean,
    int CyclesTotal,
    int CyclesUndetermined);

public sealed record AnalysisResult(
    AnalysisSummary Summary,
    IReadOnlyList<DetectedAnomaly> Anomalies,
    IReadOnlyList<FilteredSampleWrite> FilteredSamples,
    IReadOnlyList<FiducialWrite> Fiducials);

public readonly record struct FilteredSampleWrite(long OffsetMs, float ValueUv);

public readonly record struct FiducialWrite(
    int CycleIdx,
    int SegmentIdx,
    long RMs,
    long? QOnMs,
    long? SOffMs,
    long? POnMs,
    long? POffMs,
    long? TOffMs,
    long? JMs,
    float BaselineUv,
    float RAmpUv);
