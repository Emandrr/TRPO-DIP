using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SaEkg.Domain.Constants;
using SaEkg.Domain.Enums;

namespace SaEkg.Application.Csv;

public sealed partial class CsvValidator
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public CsvValidationResult Validate(Stream stream, string fileName, long? declaredLengthBytes = null)
    {
        if (!fileName.EndsWith(CsvValidationConstants.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return CsvValidationResult.Fail(1, "Файл должен иметь расширение .csv.");
        }

        if (declaredLengthBytes is > CsvValidationConstants.MaxFileBytes)
        {
            return CsvValidationResult.Fail(1, "Размер файла превышает 1 ГБ.");
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var bomProbe = new byte[3];
        var read = stream.Read(bomProbe, 0, 3);
        if (read >= 3 && bomProbe[0] == 0xEF && bomProbe[1] == 0xBB && bomProbe[2] == 0xBF)
        {
            return CsvValidationResult.Fail(2, "UTF-8 BOM не допускается.");
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
        else if (read > 0)
        {
            return CsvValidationResult.Fail(2, "Поток должен поддерживать Seek для проверки UTF-8.");
        }

        using var reader = new StreamReader(stream, StrictUtf8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        string? headerLine;
        try
        {
            headerLine = reader.ReadLine();
        }
        catch (DecoderFallbackException)
        {
            return CsvValidationResult.Fail(2, "Файл должен быть в кодировке UTF-8.");
        }

        if (headerLine is null)
        {
            return CsvValidationResult.Fail(3, "Отсутствует заголовок CSV.", 1);
        }

        var headerCols = SplitFields(headerLine);
        if (!IsValidHeader(headerCols, out var columnMap))
        {
            return CsvValidationResult.Fail(3, "Заголовок должен содержать ровно столбцы value, timestamp, errors.", 1);
        }

        DateTime? firstTs = null;
        DateTime? lastTs = null;
        DateTime? prevTs = null;
        string? prevValue = null;
        string? prevErrors = null;
        long uniqueCount = 0;
        long unusableMs = 0;
        var gaps = new List<CsvGapInfo>();
        var intervalHist = new Dictionary<long, int>();
        var lineNumber = 1;

        while (true)
        {
            string? line;
            try
            {
                line = reader.ReadLine();
            }
            catch (DecoderFallbackException)
            {
                return CsvValidationResult.Fail(2, "Файл должен быть в кодировке UTF-8.");
            }

            if (line is null)
            {
                break;
            }

            lineNumber++;
            if (line.Length == 0)
            {
                return CsvValidationResult.Fail(4, "Пустая строка данных недопустима.", lineNumber);
            }

            var fields = SplitFields(line);
            if (fields.Length != 3)
            {
                return CsvValidationResult.Fail(4, "В каждой строке должно быть ровно 3 поля.", lineNumber);
            }

            if (fields.Any(static f => f.Contains('"')))
            {
                return CsvValidationResult.Fail(4, "Кавычки в полях CSV не допускаются.", lineNumber);
            }

            var valueRaw = fields[columnMap["value"]];
            var tsRaw = fields[columnMap["timestamp"]];
            var errorsRaw = fields[columnMap["errors"]];

            if (!IntegerPattern().IsMatch(valueRaw)
                || !int.TryParse(valueRaw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
                || value < CsvValidationConstants.MinValue
                || value > CsvValidationConstants.MaxValue)
            {
                return CsvValidationResult.Fail(5, "Некорректное значение value.", lineNumber);
            }

            if (!TimestampPattern().IsMatch(tsRaw)
                || !DateTime.TryParseExact(
                    tsRaw,
                    "yyyy-MM-dd HH:mm:ss.fff",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var ts))
            {
                return CsvValidationResult.Fail(6, "Некорректный timestamp.", lineNumber);
            }

            if (errorsRaw is not (CsvValidationConstants.ErrorValue or CsvValidationConstants.NoErrorValue))
            {
                return CsvValidationResult.Fail(7, "Поле errors должно быть Error или NoError.", lineNumber);
            }

            if (prevTs is not null)
            {
                if (ts < prevTs.Value)
                {
                    return CsvValidationResult.Fail(8, "Timestamp не должен уменьшаться.", lineNumber);
                }

                if (ts == prevTs.Value)
                {
                    if (valueRaw != prevValue || errorsRaw != prevErrors)
                    {
                        return CsvValidationResult.Fail(9, "Одинаковый timestamp с разными value/errors.", lineNumber);
                    }

                    continue;
                }

                var delta = (long)(ts - prevTs.Value).TotalMilliseconds;
                intervalHist[delta] = intervalHist.GetValueOrDefault(delta) + 1;
                if (delta > CsvValidationConstants.SampleIntervalMs)
                {
                    var startOffset = (long)(prevTs.Value - firstTs!.Value).TotalMilliseconds + CsvValidationConstants.SampleIntervalMs;
                    var endOffset = (long)(ts - firstTs.Value).TotalMilliseconds;
                    gaps.Add(new CsvGapInfo(startOffset, endOffset, GapReason.Gap));
                    unusableMs += delta - CsvValidationConstants.SampleIntervalMs;
                }
            }

            firstTs ??= ts;
            lastTs = ts;
            prevTs = ts;
            prevValue = valueRaw;
            prevErrors = errorsRaw;
            uniqueCount++;

            if (errorsRaw == CsvValidationConstants.ErrorValue)
            {
                var offset = (long)(ts - firstTs.Value).TotalMilliseconds;
                gaps.Add(new CsvGapInfo(offset, offset + CsvValidationConstants.SampleIntervalMs, GapReason.Error));
                unusableMs += CsvValidationConstants.SampleIntervalMs;
            }
        }

        if (uniqueCount == 0 || firstTs is null || lastTs is null)
        {
            return CsvValidationResult.Fail(11, "Недостаточно данных для проверки длительности.");
        }

        var median = ComputeMedianInterval(intervalHist);
        if (uniqueCount > 1 && median != CsvValidationConstants.SampleIntervalMs)
        {
            return CsvValidationResult.Fail(10, "Медианный интервал между отсчётами должен быть 4 мс.");
        }

        var durationMs = (long)(lastTs.Value - firstTs.Value).TotalMilliseconds + CsvValidationConstants.SampleIntervalMs;
        if (durationMs < CsvValidationConstants.MinDurationMs || durationMs > CsvValidationConstants.MaxDurationMs)
        {
            return CsvValidationResult.Fail(11, "Длительность записи должна быть от 10 с до 24 ч.");
        }

        var ratio = (decimal)unusableMs / durationMs;
        if (ratio > (decimal)CsvValidationConstants.MaxUnusableRatio)
        {
            return CsvValidationResult.Fail(12, "Доля непригодных данных превышает 30%.");
        }

        return CsvValidationResult.Success(uniqueCount, firstTs.Value, lastTs.Value, decimal.Round(ratio, 4), gaps);
    }

    private static bool IsValidHeader(string[] cols, out Dictionary<string, int> map)
    {
        map = new Dictionary<string, int>(StringComparer.Ordinal);
        if (cols.Length != 3)
        {
            return false;
        }

        var required = new HashSet<string>(StringComparer.Ordinal) { "value", "timestamp", "errors" };
        for (var i = 0; i < cols.Length; i++)
        {
            var name = cols[i].Trim();
            if (!required.Contains(name) || map.ContainsKey(name))
            {
                return false;
            }

            map[name] = i;
        }

        return map.Count == 3;
    }

    private static string[] SplitFields(string line) => line.Split(',');

    private static long ComputeMedianInterval(Dictionary<long, int> hist)
    {
        if (hist.Count == 0)
        {
            return CsvValidationConstants.SampleIntervalMs;
        }

        var total = hist.Values.Sum();
        var mid1 = (total - 1) / 2;
        var mid2 = total / 2;
        long seen = 0;
        long? a = null;
        long? b = null;
        foreach (var key in hist.Keys.OrderBy(x => x))
        {
            seen += hist[key];
            if (a is null && seen > mid1)
            {
                a = key;
            }

            if (b is null && seen > mid2)
            {
                b = key;
                break;
            }
        }

        return (a!.Value + b!.Value) / 2;
    }

    [GeneratedRegex(@"^-?\d+$")]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}$")]
    private static partial Regex TimestampPattern();
}
