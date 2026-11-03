namespace SaEkg.Application.Analysis;

public sealed class BiquadFilter
{
    private readonly double _b0, _b1, _b2, _a1, _a2;
    private double _z1, _z2;

    public BiquadFilter(double b0, double b1, double b2, double a0, double a1, double a2)
    {
        _b0 = b0 / a0;
        _b1 = b1 / a0;
        _b2 = b2 / a0;
        _a1 = a1 / a0;
        _a2 = a2 / a0;
    }

    public void Reset()
    {
        _z1 = 0;
        _z2 = 0;
    }

    public double Process(double x)
    {
        var y = _b0 * x + _z1;
        _z1 = _b1 * x - _a1 * y + _z2;
        _z2 = _b2 * x - _a2 * y;
        return y;
    }

    public BiquadFilter CloneClean() => new(_b0, _b1, _b2, 1, _a1, _a2);

    public static BiquadFilter LowPass(double sampleRate, double cutoffHz, double q = 0.70710678118)
    {
        var w0 = 2 * Math.PI * cutoffHz / sampleRate;
        var alpha = Math.Sin(w0) / (2 * q);
        var cosw = Math.Cos(w0);
        return new BiquadFilter((1 - cosw) / 2, 1 - cosw, (1 - cosw) / 2, 1 + alpha, -2 * cosw, 1 - alpha);
    }

    public static BiquadFilter HighPass(double sampleRate, double cutoffHz, double q = 0.70710678118)
    {
        var w0 = 2 * Math.PI * cutoffHz / sampleRate;
        var alpha = Math.Sin(w0) / (2 * q);
        var cosw = Math.Cos(w0);
        return new BiquadFilter((1 + cosw) / 2, -(1 + cosw), (1 + cosw) / 2, 1 + alpha, -2 * cosw, 1 - alpha);
    }

    public static BiquadFilter Notch(double sampleRate, double freqHz, double q = 30)
    {
        var w0 = 2 * Math.PI * freqHz / sampleRate;
        var alpha = Math.Sin(w0) / (2 * q);
        var cosw = Math.Cos(w0);
        return new BiquadFilter(1, -2 * cosw, 1, 1 + alpha, -2 * cosw, 1 - alpha);
    }

    public static BiquadFilter BandPass(double sampleRate, double lowHz, double highHz)
    {
        var center = Math.Sqrt(lowHz * highHz);
        var bw = highHz - lowHz;
        var q = center / Math.Max(bw, 0.1);
        var w0 = 2 * Math.PI * center / sampleRate;
        var alpha = Math.Sin(w0) / (2 * q);
        var cosw = Math.Cos(w0);
        return new BiquadFilter(alpha, 0, -alpha, 1 + alpha, -2 * cosw, 1 - alpha);
    }
}

public static class FiltFilt
{
    public static float[] Apply(float[] input, Func<BiquadFilter[]> stagesFactory)
    {
        var forward = Process(input, stagesFactory());
        Array.Reverse(forward);
        var backward = Process(forward, stagesFactory());
        Array.Reverse(backward);
        return backward;
    }

    private static float[] Process(float[] input, BiquadFilter[] stages)
    {
        var output = new float[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            double y = input[i];
            foreach (var stage in stages)
            {
                y = stage.Process(y);
            }

            output[i] = (float)y;
        }

        return output;
    }
}
