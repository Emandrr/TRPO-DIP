using SaEkg.Domain.Constants;

namespace SaEkg.Application.Analysis;

public static class PanTompkinsQrsDetector
{
    public static IReadOnlyList<long> Detect(SignalSegment segment)
    {
        var fs = AnalysisConstants.SampleRateHz;
        var filtered = segment.ValuesMv;
        var band = FiltFilt.Apply(filtered, () => [BiquadFilter.BandPass(fs, 5, 15)]);
        var diff = Differentiate(band);
        var squared = diff.Select(x => x * x).ToArray();
        var integrated = MovingIntegrate(squared, (int)(0.15 * fs));

        var peaks = FindPeaks(integrated, fs);
        var rPeaks = new List<long>();
        foreach (var peakIdx in peaks)
        {
            var refined = RefineR(filtered, peakIdx, fs);
            var rMs = segment.StartOffsetMs + refined * AnalysisConstants.SampleIntervalMs;
            if (rPeaks.Count == 0 || rMs - rPeaks[^1] >= 200)
            {
                rPeaks.Add(rMs);
            }
        }

        return rPeaks;
    }

    private static float[] Differentiate(float[] x)
    {
        var y = new float[x.Length];
        for (var i = 2; i < x.Length - 2; i++)
        {
            y[i] = (2 * x[i + 2] + x[i + 1] - x[i - 1] - 2 * x[i - 2]) / 8f;
        }

        return y;
    }

    private static float[] MovingIntegrate(float[] x, int window)
    {
        var y = new float[x.Length];
        double sum = 0;
        for (var i = 0; i < x.Length; i++)
        {
            sum += x[i];
            if (i >= window)
            {
                sum -= x[i - window];
            }

            y[i] = (float)(sum / Math.Min(i + 1, window));
        }

        return y;
    }

    private static List<int> FindPeaks(float[] integrated, int fs)
    {
        var peaks = new List<int>();
        if (integrated.Length < fs)
        {
            return peaks;
        }

        double spki = 0, npki = 0;
        var threshold = 0.0;
        var rrAvg = 0.0;
        var lastPeak = -fs;
        var initEnd = Math.Min(integrated.Length, 2 * fs);
        var initMax = integrated.Take(initEnd).Max();
        spki = initMax * 0.25;
        npki = initMax * 0.125;
        threshold = npki + 0.25 * (spki - npki);

        for (var i = 1; i < integrated.Length - 1; i++)
        {
            if (integrated[i] > integrated[i - 1] && integrated[i] >= integrated[i + 1] && integrated[i] > threshold)
            {
                if (i - lastPeak < (int)(0.2 * fs))
                {
                    continue;
                }

                peaks.Add(i);
                spki = 0.125 * integrated[i] + 0.875 * spki;
                threshold = npki + 0.25 * (spki - npki);
                if (lastPeak >= 0)
                {
                    var rr = (i - lastPeak) * 1000.0 / fs;
                    rrAvg = rrAvg == 0 ? rr : 0.125 * rr + 0.875 * rrAvg;
                }

                lastPeak = i;
            }
            else if (integrated[i] > integrated[i - 1] && integrated[i] >= integrated[i + 1])
            {
                npki = 0.125 * integrated[i] + 0.875 * npki;
                threshold = npki + 0.25 * (spki - npki);
            }

            if (rrAvg > 0 && lastPeak >= 0 && (i - lastPeak) * 1000.0 / fs > 1.66 * rrAvg)
            {
                var searchFrom = lastPeak + (int)(0.2 * fs);
                var searchTo = Math.Min(i, lastPeak + (int)(rrAvg * fs / 1000.0));
                if (searchTo > searchFrom)
                {
                    var maxIdx = searchFrom;
                    for (var j = searchFrom; j <= searchTo; j++)
                    {
                        if (integrated[j] > integrated[maxIdx])
                        {
                            maxIdx = j;
                        }
                    }

                    if (integrated[maxIdx] > threshold * 0.5)
                    {
                        peaks.Add(maxIdx);
                        lastPeak = maxIdx;
                        spki = 0.125 * integrated[maxIdx] + 0.875 * spki;
                        threshold = npki + 0.25 * (spki - npki);
                    }
                }
            }
        }

        return peaks;
    }

    private static int RefineR(float[] signal, int approxIdx, int fs)
    {
        var half = (int)(0.075 * fs);
        var from = Math.Max(0, approxIdx - half);
        var to = Math.Min(signal.Length - 1, approxIdx + half);
        var best = from;
        var bestAbs = Math.Abs(signal[from]);
        for (var i = from; i <= to; i++)
        {
            var a = Math.Abs(signal[i]);
            if (a > bestAbs)
            {
                bestAbs = a;
                best = i;
            }
        }

        return best;
    }
}
