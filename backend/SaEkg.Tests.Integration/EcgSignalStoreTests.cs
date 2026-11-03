using FluentAssertions;
using Microsoft.Extensions.Options;
using SaEkg.Application.Ecg;
using SaEkg.Infrastructure.DuckDb;
using Xunit;

namespace SaEkg.Tests.Integration;

public sealed class EcgSignalStoreTests
{
    [Fact]
    public async Task Append_one_million_filtered_samples_and_read_range()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saekg-duckdb-{Guid.NewGuid():N}.duckdb");
        await using var store = new EcgSignalStore(Options.Create(new DuckDbOptions { Path = path }));
        var recordId = Guid.NewGuid();
        const int count = 1_000_000;

        var samples = new FilteredSamplePoint[count];
        for (var i = 0; i < count; i++)
        {
            samples[i] = new FilteredSamplePoint(i * 4L, i % 1000);
        }

        var swWrite = System.Diagnostics.Stopwatch.StartNew();
        await store.AppendFilteredSamplesAsync(recordId, samples);
        swWrite.Stop();
        swWrite.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));

        var swRead = System.Diagnostics.Stopwatch.StartNew();
        var points = await store.ReadFilteredDownsampledAsync(recordId, 0, count * 4L, 2000);
        swRead.Stop();
        swRead.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(500));

        points.Should().NotBeEmpty();
        points.Count.Should().BeLessThanOrEqualTo(2000);

        await store.DeleteByRecordIdAsync(recordId);
        var afterDelete = await store.ReadFilteredDownsampledAsync(recordId, 0, count * 4L, 100);
        afterDelete.Should().BeEmpty();

        try
        {
            File.Delete(path);
            File.Delete(path + ".wal");
        }
        catch
        {
        }
    }
}
