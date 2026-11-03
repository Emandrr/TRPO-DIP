using System.Globalization;
using System.Text;

namespace SaEkg.Tests.Unit.Csv;

internal static class CsvTestData
{
    public static MemoryStream Build(
        int sampleCount,
        int intervalMs = 4,
        int? errorEvery = null,
        int? gapAfterIndex = null,
        int gapMs = 20,
        string header = "timestamp,value,errors",
        Func<int, string>? valueFactory = null,
        bool includeBom = false,
        string? lineEnding = "\n")
    {
        var sb = new StringBuilder();
        if (includeBom)
        {
            sb.Append('\uFEFF');
        }

        sb.Append(header).Append(lineEnding);
        var start = new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified);
        long offsetMs = 0;
        for (var i = 0; i < sampleCount; i++)
        {
            if (gapAfterIndex is not null && i == gapAfterIndex.Value + 1)
            {
                offsetMs += gapMs - intervalMs;
            }

            var ts = start.AddMilliseconds(offsetMs);
            var value = valueFactory?.Invoke(i) ?? "100";
            var errors = errorEvery is not null && i % errorEvery == 0 ? "Error" : "NoError";
            sb.Append(CultureInfo.InvariantCulture, $"{ts:yyyy-MM-dd HH:mm:ss.fff},{value},{errors}").Append(lineEnding);
            offsetMs += intervalMs;
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public static MemoryStream FromText(string text, bool utf8Bom = false)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        if (!utf8Bom)
        {
            return new MemoryStream(bytes);
        }

        var withBom = new byte[bytes.Length + 3];
        withBom[0] = 0xEF;
        withBom[1] = 0xBB;
        withBom[2] = 0xBF;
        Buffer.BlockCopy(bytes, 0, withBom, 3, bytes.Length);
        return new MemoryStream(withBom);
    }
}
