using FluentAssertions;
using SaEkg.Application.Csv;
using Xunit;

namespace SaEkg.Tests.Unit.Csv;

public class EcgCsvGeneratorTests
{
    [Fact]
    public void Generator_produces_file_accepted_by_validator()
    {
        using var stream = EcgCsvGenerator.Generate(TimeSpan.FromSeconds(10), heartRateBpm: 72);
        var result = new CsvValidator().Validate(stream, "gen.csv");
        result.Ok.Should().BeTrue();
        result.SampleCount.Should().Be(2500);
    }
}
