using SaEkg.Application.Csv;
using SaEkg.Application.Ecg;
using SaEkg.Domain.Constants;

namespace SaEkg.Application.Analysis;

public static class Segmenter
{
    public static IReadOnlyList<SignalSegment> BuildSegments(
        IReadOnlyList<RawSamplePoint> samples,
        IReadOnlyList<CsvGapInfo> gaps)
    {
        if (samples.Count == 0)
        {
            return Array.Empty<SignalSegment>();
        }

        var blocked = gaps
            .Select(g => (g.StartOffsetMs, g.EndOffsetMs))
            .OrderBy(x => x.StartOffsetMs)
            .ToList();

        var segments = new List<SignalSegment>();
        var current = new List<RawSamplePoint>();

        void Flush()
        {
            if (current.Count == 0)
            {
                return;
            }

            var start = current[0].OffsetMs;
            var end = current[^1].OffsetMs + AnalysisConstants.SampleIntervalMs;
            var durationSec = (end - start) / 1000.0;
            if (durationSec >= AnalysisConstants.MinSegmentSec)
            {
                var values = current.Select(s => s.Value / 1000f).ToArray();
                segments.Add(new SignalSegment(start, end, values));
            }

            current = new List<RawSamplePoint>();
        }

        foreach (var sample in samples.OrderBy(s => s.OffsetMs))
        {
            var inGap = blocked.Any(g => sample.OffsetMs >= g.StartOffsetMs && sample.OffsetMs < g.EndOffsetMs);
            if (inGap)
            {
                Flush();
                continue;
            }

            if (current.Count > 0)
            {
                var prev = current[^1].OffsetMs;
                if (sample.OffsetMs - prev > AnalysisConstants.SampleIntervalMs)
                {
                    Flush();
                }
            }

            current.Add(sample);
        }

        Flush();
        return segments;
    }
}
