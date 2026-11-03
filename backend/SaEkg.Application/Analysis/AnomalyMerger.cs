using SaEkg.Domain.Constants;

namespace SaEkg.Application.Analysis;

public static class AnomalyMerger
{
    public static List<DetectedAnomaly> Merge(IEnumerable<DetectedAnomaly> anomalies)
    {
        var groups = anomalies
            .GroupBy(a => (a.IndicatorCode, a.Direction))
            .SelectMany(MergeGroup)
            .OrderBy(a => a.StartOffsetMs)
            .ToList();
        return groups;
    }

    private static IEnumerable<DetectedAnomaly> MergeGroup(IGrouping<(string Code, string Direction), DetectedAnomaly> group)
    {
        var ordered = group.OrderBy(a => a.StartOffsetMs).ToList();
        if (ordered.Count == 0)
        {
            yield break;
        }

        var cur = ordered[0];
        for (var i = 1; i < ordered.Count; i++)
        {
            var next = ordered[i];
            if (next.StartOffsetMs - cur.EndOffsetMs <= AnalysisConstants.AnomalyMergeGapMs)
            {
                cur = cur with
                {
                    EndOffsetMs = Math.Max(cur.EndOffsetMs, next.EndOffsetMs),
                    ValueMin = Min(cur.ValueMin, next.ValueMin),
                    ValueMax = Max(cur.ValueMax, next.ValueMax),
                    CycleCount = cur.CycleCount + next.CycleCount
                };
            }
            else
            {
                yield return cur;
                cur = next;
            }
        }

        yield return cur;
    }

    private static double? Min(double? a, double? b) =>
        a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);

    private static double? Max(double? a, double? b) =>
        a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);
}
