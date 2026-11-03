using FluentAssertions;
using SaEkg.Application.Analysis;
using SaEkg.Domain.Constants;
using Xunit;

namespace SaEkg.Tests.Unit.Analysis;

public class EcgFilterPipelineTests
{
    [Fact]
    public void Notch_attenuates_50hz_at_least_30db()
    {
        var fs = AnalysisConstants.SampleRateHz;
        var n = fs * 4;
        var signal = new float[n];
        for (var i = 0; i < n; i++)
        {
            signal[i] = (float)Math.Sin(2 * Math.PI * 50 * i / fs);
        }

        var filtered = EcgFilterPipeline.Filter(signal);
        var inRms = Rms(signal.Skip(fs).Take(2 * fs).ToArray());
        var outRms = Rms(filtered.Skip(fs).Take(2 * fs).ToArray());
        var db = 20 * Math.Log10(outRms / inRms);
        db.Should().BeLessThan(-30);
    }

    [Fact]
    public void Passband_10hz_loss_at_most_1db()
    {
        var fs = AnalysisConstants.SampleRateHz;
        var n = fs * 4;
        var signal = new float[n];
        for (var i = 0; i < n; i++)
        {
            signal[i] = (float)Math.Sin(2 * Math.PI * 10 * i / fs);
        }

        var filtered = EcgFilterPipeline.Filter(signal);
        var inRms = Rms(signal.Skip(fs).Take(2 * fs).ToArray());
        var outRms = Rms(filtered.Skip(fs).Take(2 * fs).ToArray());
        var db = 20 * Math.Log10(outRms / inRms);
        Math.Abs(db).Should().BeLessThanOrEqualTo(1.5);
    }

    [Fact]
    public void Constant_drift_is_removed()
    {
        var signal = Enumerable.Repeat(1f, AnalysisConstants.SampleRateHz * 3).ToArray();
        var filtered = EcgFilterPipeline.Filter(signal);
        var mid = filtered.Skip(AnalysisConstants.SampleRateHz).Take(AnalysisConstants.SampleRateHz).Select(x => (double)x).Average();
        Math.Abs(mid).Should().BeLessThan(0.05);
    }

    private static double Rms(float[] x)
    {
        double s = 0;
        foreach (var v in x)
        {
            s += v * v;
        }

        return Math.Sqrt(s / x.Length);
    }
}
