using System.Diagnostics;
using FluentAssertions;
using SaEkg.Application.Analysis;
using SaEkg.Application.Csv;
using SaEkg.Application.Ecg;
using SaEkg.Domain.Enums;
using Xunit;

namespace SaEkg.Tests.Unit.Analysis;

public class AnalysisPerfSmokeTests
{
    [Fact]
    public void Analyze_one_minute_synthetic_signal_under_limits()
    {
        using var csv = EcgCsvGenerator.Generate(TimeSpan.FromMinutes(1), heartRateBpm: 72);
        var validation = new CsvValidator().Validate(csv, "m1.csv");
        validation.Ok.Should().BeTrue();

        csv.Position = 0;
        var samples = ReadSamples(csv);
        var sw = Stopwatch.StartNew();
        var before = GC.GetTotalMemory(true);
        var result = EcgAnalyzer.Analyze(samples, validation.Gaps, Sex.Male);
        sw.Stop();
        var after = GC.GetTotalMemory(false);

        result.Fiducials.Should().NotBeEmpty();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20));
        (after - before).Should().BeLessThan(400L * 1024 * 1024);
    }

    [Fact]
    public void Analyze_optional_24h_guarded_by_env()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SAEKG_PERF"), "1", StringComparison.Ordinal))
        {
            return;
        }

        using var csv = EcgCsvGenerator.Generate(TimeSpan.FromHours(24), heartRateBpm: 70);
        var validation = new CsvValidator().Validate(csv, "d24.csv");
        validation.Ok.Should().BeTrue();
        csv.Position = 0;
        var samples = ReadSamples(csv);
        var before = GC.GetTotalMemory(true);
        var sw = Stopwatch.StartNew();
        var result = EcgAnalyzer.Analyze(samples, validation.Gaps, Sex.Male);
        sw.Stop();
        var after = GC.GetTotalMemory(false);
        result.Fiducials.Should().NotBeEmpty();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(15));
        (after - before).Should().BeLessThan(1500L * 1024 * 1024);
    }

    private static List<RawSamplePoint> ReadSamples(Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        _ = reader.ReadLine();
        var list = new List<RawSamplePoint>();
        string? line;
        long i = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            var parts = line.Split(',');
            if (parts[2] == "Error")
            {
                continue;
            }

            var ts = DateTime.ParseExact(parts[0], "yyyy-MM-dd HH:mm:ss.fff", null);
            list.Add(new RawSamplePoint(i * 4, ts, short.Parse(parts[1])));
            i++;
        }

        return list;
    }
}
