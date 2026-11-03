using SaEkg.Application.Csv;
using SaEkg.Application.Ecg;
using SaEkg.Domain.Constants;

namespace SaEkg.Application.Analysis;

public static class EcgAnalyzer
{
    public static AnalysisResult Analyze(
        IReadOnlyList<RawSamplePoint> rawSamples,
        IReadOnlyList<CsvGapInfo> gaps,
        string sex)
    {
        var segments = Segmenter.BuildSegments(rawSamples, gaps);
        var filteredWrites = new List<FilteredSampleWrite>();
        var fiducialWrites = new List<FiducialWrite>();
        var allR = new List<long>();
        var allFiducials = new List<FiducialPoints>();
        var edgeMs = AnalysisConstants.EdgeDiscardSec * 1000L;

        for (var sIdx = 0; sIdx < segments.Count; sIdx++)
        {
            var segment = segments[sIdx];
            var filtered = EcgFilterPipeline.Filter(segment.ValuesMv);
            for (var i = 0; i < filtered.Length; i++)
            {
                var offset = segment.StartOffsetMs + i * (long)AnalysisConstants.SampleIntervalMs;
                filteredWrites.Add(new FilteredSampleWrite(offset, filtered[i] * 1000f));
            }

            var usable = new SignalSegment(
                segment.StartOffsetMs + edgeMs,
                Math.Max(segment.StartOffsetMs + edgeMs, segment.EndOffsetMs - edgeMs),
                Extract(filtered, edgeMs, segment));

            if (usable.ValuesMv.Length < AnalysisConstants.MinSegmentSec * AnalysisConstants.SampleRateHz)
            {
                continue;
            }

            var rPeaks = PanTompkinsQrsDetector.Detect(usable);
            allR.AddRange(rPeaks);
            var fiducials = FiducialDetector.Detect(usable, rPeaks);
            allFiducials.AddRange(fiducials);
            for (var c = 0; c < fiducials.Count; c++)
            {
                var f = fiducials[c];
                fiducialWrites.Add(new FiducialWrite(
                    c,
                    sIdx,
                    f.RMs,
                    f.QOnMs,
                    f.SOffMs,
                    f.POnMs,
                    f.POffMs,
                    f.TOffMs,
                    f.JMs,
                    f.BaselineMv * 1000f,
                    f.RAmpMv * 1000f));
            }
        }

        allR.Sort();
        var (summary, anomalies) = MetricEngine.Analyze(allR, allFiducials, sex);
        return new AnalysisResult(summary, anomalies, filteredWrites, fiducialWrites);
    }

    private static float[] Extract(float[] filtered, long edgeMs, SignalSegment segment)
    {
        var edgeSamples = (int)(edgeMs / AnalysisConstants.SampleIntervalMs);
        if (filtered.Length <= edgeSamples * 2)
        {
            return Array.Empty<float>();
        }

        var len = filtered.Length - edgeSamples * 2;
        var slice = new float[len];
        Array.Copy(filtered, edgeSamples, slice, 0, len);
        return slice;
    }
}
