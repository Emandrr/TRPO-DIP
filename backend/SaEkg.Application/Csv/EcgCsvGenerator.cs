using System.Globalization;
using System.Text;

namespace SaEkg.Application.Csv;

public static class EcgCsvGenerator
{
    public static MemoryStream Generate(
        TimeSpan duration,
        int heartRateBpm = 60,
        int intervalMs = 4,
        double noiseAmplitude = 0,
        IReadOnlyCollection<int>? errorSampleIndexes = null,
        IReadOnlyDictionary<int, int>? gapsAfterIndexMs = null)
    {
        var sampleCount = (int)(duration.TotalMilliseconds / intervalMs);
        var sb = new StringBuilder(sampleCount * 48);
        sb.Append("timestamp,value,errors\n");
        var start = new DateTime(2026, 1, 1, 8, 0, 0, 0, DateTimeKind.Unspecified);
        var rrMs = 60_000.0 / heartRateBpm;
        long offset = 0;
        var errors = errorSampleIndexes is null ? null : new HashSet<int>(errorSampleIndexes);

        for (var i = 0; i < sampleCount; i++)
        {
            if (gapsAfterIndexMs is not null && gapsAfterIndexMs.TryGetValue(i - 1, out var gapMs) && i > 0)
            {
                offset += gapMs - intervalMs;
            }

            var ts = start.AddMilliseconds(offset);
            var phase = (offset % (long)rrMs) / rrMs;
            var value = SyntheticMv(phase);
            if (noiseAmplitude > 0)
            {
                value += Math.Sin(i * 0.37) * noiseAmplitude;
            }

            var adc = (int)Math.Clamp(Math.Round(value * 1000), -5000, 5000);
            var err = errors is not null && errors.Contains(i) ? "Error" : "NoError";
            sb.Append(CultureInfo.InvariantCulture, $"{ts:yyyy-MM-dd HH:mm:ss.fff},{adc},{err}\n");
            offset += intervalMs;
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static double SyntheticMv(double phase)
    {
        return Gauss(phase, 0.18, 0.025, 0.15)
               + Gauss(phase, 0.37, 0.008, -0.12)
               + Gauss(phase, 0.40, 0.009, 1.0)
               + Gauss(phase, 0.43, 0.009, -0.25)
               + Gauss(phase, 0.66, 0.05, 0.30);
    }

    private static double Gauss(double x, double c, double w, double a) =>
        a * Math.Exp(-((x - c) * (x - c)) / (2 * w * w));
}
