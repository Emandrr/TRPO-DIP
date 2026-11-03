using FluentAssertions;
using SaEkg.Application.Analysis;
using SaEkg.Domain.Constants;
using Xunit;

namespace SaEkg.Tests.Unit.Analysis;

public class QrsDetectorTests
{
    [Fact]
    public void Detects_synthetic_r_peaks_within_8ms()
    {
        var fs = AnalysisConstants.SampleRateHz;
        var rrMs = 800;
        var durationSec = 20;
        var n = fs * durationSec;
        var signal = new float[n];
        var expected = new List<long>();
        for (var tMs = 200; tMs < durationSec * 1000 - 200; tMs += rrMs)
        {
            expected.Add(tMs);
            var center = tMs / AnalysisConstants.SampleIntervalMs;
            for (var k = -5; k <= 5; k++)
            {
                var idx = center + k;
                if (idx >= 0 && idx < n)
                {
                    signal[idx] += (float)Math.Exp(-(k * k) / 4.0);
                }
            }
        }

        var segment = new SignalSegment(0, durationSec * 1000L, signal);
        var detected = PanTompkinsQrsDetector.Detect(segment);
        detected.Count.Should().BeGreaterThan(expected.Count / 2);
        foreach (var e in expected.Skip(1).SkipLast(1))
        {
            detected.Min(d => Math.Abs(d - e)).Should().BeLessThanOrEqualTo(8);
        }
    }
}
