using FluentAssertions;
using SaEkg.Application.Csv;
using Xunit;

namespace SaEkg.Tests.Unit.Csv;

public class CsvSampleReaderTests
{
    [Fact]
    public async Task Reads_Error_for_timeline_but_skips_raw_samples()
    {
        var csv =
            "timestamp,value,errors\n" +
            "2026-01-01 00:00:00.000,10,Error\n" +
            "2026-01-01 00:00:00.004,11,Error\n" +
            "2026-01-01 00:00:00.008,98,NoError\n" +
            "2026-01-01 00:00:00.012,99,NoError\n";

        await using var stream = CsvTestData.FromText(csv);
        var batches = new List<SaEkg.Application.Ecg.RawSamplePoint>();
        await foreach (var batch in CsvSampleReader.ReadRawBatchesAsync(stream))
        {
            batches.AddRange(batch);
        }

        batches.Should().HaveCount(2);
        batches[0].OffsetMs.Should().Be(8);
        batches[0].Value.Should().Be(98);
        batches[1].OffsetMs.Should().Be(12);
        batches[1].Value.Should().Be(99);
    }

    [Fact]
    public async Task Skips_duplicate_timestamps_including_Error()
    {
        var csv =
            "timestamp,value,errors\n" +
            "2026-01-01 00:00:00.000,10,Error\n" +
            "2026-01-01 00:00:00.000,10,Error\n" +
            "2026-01-01 00:00:00.004,98,NoError\n";

        await using var stream = CsvTestData.FromText(csv);
        var batches = new List<SaEkg.Application.Ecg.RawSamplePoint>();
        await foreach (var batch in CsvSampleReader.ReadRawBatchesAsync(stream))
        {
            batches.AddRange(batch);
        }

        batches.Should().ContainSingle();
        batches[0].OffsetMs.Should().Be(4);
        batches[0].Value.Should().Be(98);
    }
}
