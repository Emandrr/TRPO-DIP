using SaEkg.Domain.Constants;

namespace SaEkg.Application.Analysis;

public static class EcgFilterPipeline
{
    public static float[] Filter(float[] valuesMv)
    {
        var fs = AnalysisConstants.SampleRateHz;
        var afterHp = FiltFilt.Apply(valuesMv, () =>
        [
            BiquadFilter.HighPass(fs, 0.5),
            BiquadFilter.HighPass(fs, 0.5)
        ]);
        var afterLp = FiltFilt.Apply(afterHp, () =>
        [
            BiquadFilter.LowPass(fs, 40),
            BiquadFilter.LowPass(fs, 40)
        ]);
        return FiltFilt.Apply(afterLp, () => [BiquadFilter.Notch(fs, 50, 30)]);
    }
}
