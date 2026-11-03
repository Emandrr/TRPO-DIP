using System.Globalization;
using System.Text;
using SaEkg.Application.Ecg;
using SaEkg.Domain.Constants;

namespace SaEkg.Application.Csv;

public static class CsvSampleReader
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static async IAsyncEnumerable<IReadOnlyList<RawSamplePoint>> ReadRawBatchesAsync(
        Stream stream,
        int batchSize = 50_000,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var header = await reader.ReadLineAsync(cancellationToken)
            ?? throw new InvalidOperationException("CSV header missing.");
        var cols = header.Split(',');
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < cols.Length; i++)
        {
            map[cols[i].Trim()] = i;
        }

        DateTime? firstTs = null;
        DateTime? prevTs = null;
        var batch = new List<RawSamplePoint>(batchSize);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split(',');
            var valueRaw = fields[map["value"]];
            var tsRaw = fields[map["timestamp"]];
            var errorsRaw = fields[map["errors"]];

            var ts = DateTime.ParseExact(
                tsRaw,
                "yyyy-MM-dd HH:mm:ss.fff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None);
            if (prevTs is not null && ts == prevTs.Value)
            {
                continue;
            }

            firstTs ??= ts;
            prevTs = ts;

            if (errorsRaw != CsvValidationConstants.NoErrorValue)
            {
                continue;
            }

            var value = short.Parse(valueRaw, CultureInfo.InvariantCulture);
            var offsetMs = (long)(ts - firstTs.Value).TotalMilliseconds;
            batch.Add(new RawSamplePoint(offsetMs, ts, value));

            if (batch.Count >= batchSize)
            {
                yield return batch;
                batch = new List<RawSamplePoint>(batchSize);
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }
}
