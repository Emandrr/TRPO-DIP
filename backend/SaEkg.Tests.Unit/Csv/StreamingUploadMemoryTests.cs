using System.Diagnostics;
using FluentAssertions;
using SaEkg.Application.Csv;
using SaEkg.Domain.Constants;
using Xunit;

namespace SaEkg.Tests.Unit.Csv;

public class StreamingUploadMemoryTests
{
    [Fact]
    public void Validator_handles_large_inmemory_csv_under_memory_budget()
    {
        const int samples = 50_000;
        using var stream = CsvTestData.Build(samples);
        var before = GC.GetTotalMemory(forceFullCollection: true);
        var sw = Stopwatch.StartNew();
        var result = new CsvValidator().Validate(stream, "big.csv", stream.Length);
        sw.Stop();
        var after = GC.GetTotalMemory(forceFullCollection: false);

        result.Ok.Should().BeTrue(because: result.Message);
        result.SampleCount.Should().Be(samples);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
        (after - before).Should().BeLessThan(120L * 1024 * 1024);
    }

    [Fact]
    public void Declared_length_over_1gb_rejected_before_content_checks()
    {
        using var stream = CsvTestData.Build(10);
        var result = new CsvValidator().Validate(
            stream,
            "huge.csv",
            CsvValidationConstants.MaxFileBytes + 1);
        result.FailedCheck.Should().Be(1);
    }
}
