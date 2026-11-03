using SaEkg.Domain.Constants;

namespace SaEkg.Application.Analysis;

public static class FiducialDetector
{
    public static IReadOnlyList<FiducialPoints> Detect(SignalSegment segment, IReadOnlyList<long> rPeaksMs)
    {
        var result = new List<FiducialPoints>();
        if (rPeaksMs.Count < 3)
        {
            return result;
        }

        var signal = segment.ValuesMv;
        for (var i = 1; i < rPeaksMs.Count - 1; i++)
        {
            var rMs = rPeaksMs[i];
            var rIdx = OffsetToIndex(segment, rMs);
            if (rIdx is null)
            {
                continue;
            }

            var baseline = Median(signal, IndexAt(segment, rMs - 80), IndexAt(segment, rMs - 40));
            var qOnIdx = FindCrossing(signal, rIdx.Value, -1, 30, baseline, 0.05f);
            var sOffIdx = FindCrossing(signal, rIdx.Value, 1, 30, baseline, 0.05f);
            long? qOn = qOnIdx is null ? null : IndexToOffset(segment, qOnIdx.Value);
            long? sOff = sOffIdx is null ? null : IndexToOffset(segment, sOffIdx.Value);
            var j = sOff;
            var p = FindPWave(signal, segment, rMs, qOn, baseline);
            var tOff = FindTOff(signal, segment, j ?? (rMs + 40), rPeaksMs[i + 1], baseline);
            var rAmp = signal[rIdx.Value] - baseline;

            result.Add(new FiducialPoints(
                rMs,
                qOn,
                sOff,
                p.On,
                p.Off,
                tOff,
                j,
                baseline,
                rAmp));
        }

        return result;
    }

    private static int? FindCrossing(float[] signal, int from, int dir, int maxSamples, float baseline, float thr)
    {
        var end = Math.Clamp(from + dir * maxSamples, 0, signal.Length - 1);
        for (var i = from; i != end; i += dir)
        {
            if (Math.Abs(signal[i] - baseline) <= thr)
            {
                return i;
            }
        }

        return end;
    }

    private static (long? On, long? Off) FindPWave(float[] signal, SignalSegment segment, long rMs, long? qOn, float baseline)
    {
        var endMs = qOn ?? (rMs - 40);
        var startMs = rMs - 300;
        var from = IndexAt(segment, startMs);
        var to = IndexAt(segment, endMs);
        if (to <= from + 2)
        {
            return (null, null);
        }

        var peak = from;
        for (var i = from; i <= to; i++)
        {
            if (Math.Abs(signal[i] - baseline) > Math.Abs(signal[peak] - baseline))
            {
                peak = i;
            }
        }

        var ampUv = Math.Abs(signal[peak] - baseline) * 1000f;
        if (ampUv < AnalysisConstants.PMinUv)
        {
            return (null, null);
        }

        var on = FindCrossing(signal, peak, -1, 40, baseline, 0.02f) ?? from;
        var off = FindCrossing(signal, peak, 1, 40, baseline, 0.02f) ?? to;
        return (IndexToOffset(segment, on), IndexToOffset(segment, off));
    }

    private static long? FindTOff(float[] signal, SignalSegment segment, long fromMs, long nextRMs, float baseline)
    {
        var toMs = Math.Min(fromMs + 450, nextRMs - 80);
        var from = IndexAt(segment, fromMs);
        var to = IndexAt(segment, toMs);
        if (to <= from + 2)
        {
            return null;
        }

        var maxSlopeIdx = from + 1;
        var maxSlope = 0f;
        for (var i = from + 1; i < to; i++)
        {
            var slope = signal[i] - signal[i - 1];
            if (Math.Abs(slope) > Math.Abs(maxSlope))
            {
                maxSlope = slope;
                maxSlopeIdx = i;
            }
        }

        return IndexToOffset(segment, FindCrossing(signal, maxSlopeIdx, 1, to - maxSlopeIdx, baseline, 0.03f) ?? to);
    }

    private static int? OffsetToIndex(SignalSegment segment, long offsetMs)
    {
        var idx = (int)((offsetMs - segment.StartOffsetMs) / AnalysisConstants.SampleIntervalMs);
        return idx >= 0 && idx < segment.ValuesMv.Length ? idx : null;
    }

    private static int IndexAt(SignalSegment segment, long offsetMs)
    {
        var idx = (int)((offsetMs - segment.StartOffsetMs) / AnalysisConstants.SampleIntervalMs);
        return Math.Clamp(idx, 0, segment.ValuesMv.Length - 1);
    }

    private static long IndexToOffset(SignalSegment segment, int idx) =>
        segment.StartOffsetMs + idx * (long)AnalysisConstants.SampleIntervalMs;

    private static float Median(float[] signal, int from, int to)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var slice = new float[to - from + 1];
        Array.Copy(signal, from, slice, 0, slice.Length);
        Array.Sort(slice);
        return slice[slice.Length / 2];
    }
}
