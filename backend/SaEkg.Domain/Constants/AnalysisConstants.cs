namespace SaEkg.Domain.Constants;

public static class AnalysisConstants
{
    public const int SampleRateHz = 250;
    public const int SampleIntervalMs = 4;
    public const int MinSegmentSec = 5;
    public const int EdgeDiscardSec = 1;
    public const int WindowSec = 30;
    public const int WindowStepSec = 5;
    public const int AnomalyMergeGapMs = 5000;
    public const double RAmpDevPct = 30;
    public const double PAbsentFrac = 0.2;
    public const double PMinUv = 50;
    public const int MaxAnalysisAttempts = 3;
    public const int AnalysisParallelism = 2;
}
