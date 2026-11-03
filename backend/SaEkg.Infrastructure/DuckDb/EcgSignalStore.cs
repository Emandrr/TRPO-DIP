using System.Globalization;
using DuckDB.NET.Data;
using Microsoft.Extensions.Options;
using SaEkg.Application.Ecg;

namespace SaEkg.Infrastructure.DuckDb;

public sealed class EcgSignalStore : IEcgSignalStore, IAsyncDisposable
{
    private readonly SemaphoreSlim _writer = new(1, 1);
    private readonly DuckDBConnection _connection;
    private bool _schemaReady;

    public EcgSignalStore(IOptions<DuckDbOptions> options)
    {
        var path = options.Value.Path;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new DuckDBConnection($"Data Source={path}");
        _connection.Open();
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await _writer.WaitAsync(cancellationToken);
        try
        {
            EnsureSchemaCore();
        }
        finally
        {
            _writer.Release();
        }
    }

    public async Task AppendRawSamplesAsync(
        Guid recordId,
        IReadOnlyList<RawSamplePoint> samples,
        CancellationToken cancellationToken = default)
    {
        if (samples.Count == 0)
        {
            return;
        }

        await _writer.WaitAsync(cancellationToken);
        try
        {
            EnsureSchemaCore();
            using var appender = _connection.CreateAppender("raw_samples");
            foreach (var sample in samples)
            {
                cancellationToken.ThrowIfCancellationRequested();
                appender.CreateRow()
                    .AppendValue(recordId)
                    .AppendValue(sample.OffsetMs)
                    .AppendValue(sample.Ts)
                    .AppendValue(sample.Value)
                    .EndRow();
            }
        }
        finally
        {
            _writer.Release();
        }
    }

    public async Task AppendFilteredSamplesAsync(
        Guid recordId,
        IReadOnlyList<FilteredSamplePoint> samples,
        CancellationToken cancellationToken = default)
    {
        if (samples.Count == 0)
        {
            return;
        }

        await _writer.WaitAsync(cancellationToken);
        try
        {
            EnsureSchemaCore();
            using var appender = _connection.CreateAppender("filtered_samples");
            foreach (var sample in samples)
            {
                cancellationToken.ThrowIfCancellationRequested();
                appender.CreateRow()
                    .AppendValue(recordId)
                    .AppendValue(sample.OffsetMs)
                    .AppendValue(sample.ValueUv)
                    .EndRow();
            }
        }
        finally
        {
            _writer.Release();
        }
    }

    public async Task<IReadOnlyList<RawSamplePoint>> ReadRawSamplesAsync(
        Guid recordId,
        CancellationToken cancellationToken = default)
    {
        await _writer.WaitAsync(cancellationToken);
        try
        {
            EnsureSchemaCore();
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT offset_ms, ts, value
                FROM raw_samples
                WHERE record_id = $record_id
                ORDER BY offset_ms
                """;
            cmd.Parameters.Add(new DuckDBParameter("record_id", recordId));
            var result = new List<RawSamplePoint>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new RawSamplePoint(
                    reader.GetInt64(0),
                    reader.GetDateTime(1),
                    Convert.ToInt16(reader.GetValue(2), CultureInfo.InvariantCulture)));
            }

            return result;
        }
        finally
        {
            _writer.Release();
        }
    }

    public async Task AppendFiducialsAsync(
        Guid recordId,
        IReadOnlyList<FiducialSamplePoint> fiducials,
        CancellationToken cancellationToken = default)
    {
        if (fiducials.Count == 0)
        {
            return;
        }

        await _writer.WaitAsync(cancellationToken);
        try
        {
            EnsureSchemaCore();
            using var appender = _connection.CreateAppender("fiducials");
            foreach (var f in fiducials)
            {
                cancellationToken.ThrowIfCancellationRequested();
                appender.CreateRow()
                    .AppendValue(recordId)
                    .AppendValue(f.CycleIdx)
                    .AppendValue(f.SegmentIdx)
                    .AppendValue(f.RMs)
                    .AppendValue(f.QOnMs)
                    .AppendValue(f.SOffMs)
                    .AppendValue(f.POnMs)
                    .AppendValue(f.POffMs)
                    .AppendValue(f.TOffMs)
                    .AppendValue(f.JMs)
                    .AppendValue(f.BaselineUv)
                    .AppendValue(f.RAmpUv)
                    .EndRow();
            }
        }
        finally
        {
            _writer.Release();
        }
    }

    public async Task<IReadOnlyList<DownsampledPoint>> ReadFilteredDownsampledAsync(
        Guid recordId,
        long fromMs,
        long toMs,
        int maxPoints,
        CancellationToken cancellationToken = default)
    {
        if (maxPoints < 1)
        {
            maxPoints = 1;
        }

        if (toMs < fromMs)
        {
            (fromMs, toMs) = (toMs, fromMs);
        }

        await _writer.WaitAsync(cancellationToken);
        try
        {
            EnsureSchemaCore();
            var span = Math.Max(1L, toMs - fromMs);
            var bucketMs = Math.Max(1L, (long)Math.Ceiling(span / (double)maxPoints));

            await using var cmd = _connection.CreateCommand();
            cmd.CommandText =
                $"""
                SELECT
                  (offset_ms / {bucketMs}) * {bucketMs} AS bucket_start,
                  MIN(value) AS min_uv,
                  MAX(value) AS max_uv
                FROM filtered_samples
                WHERE record_id = $record_id
                  AND offset_ms >= $from_ms
                  AND offset_ms < $to_ms
                GROUP BY 1
                ORDER BY 1
                LIMIT {maxPoints}
                """;
            cmd.Parameters.Add(new DuckDBParameter("record_id", recordId));
            cmd.Parameters.Add(new DuckDBParameter("from_ms", fromMs));
            cmd.Parameters.Add(new DuckDBParameter("to_ms", toMs));

            var result = new List<DownsampledPoint>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new DownsampledPoint(
                    reader.GetInt64(0),
                    Convert.ToSingle(reader.GetValue(1), CultureInfo.InvariantCulture),
                    Convert.ToSingle(reader.GetValue(2), CultureInfo.InvariantCulture)));
            }

            return result;
        }
        finally
        {
            _writer.Release();
        }
    }

    public async Task DeleteByRecordIdAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        await _writer.WaitAsync(cancellationToken);
        try
        {
            EnsureSchemaCore();
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText =
                """
                DELETE FROM raw_samples WHERE record_id = $record_id;
                DELETE FROM filtered_samples WHERE record_id = $record_id;
                DELETE FROM fiducials WHERE record_id = $record_id;
                """;
            cmd.Parameters.Add(new DuckDBParameter("record_id", recordId));
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writer.Release();
        }
    }

    public async Task DeleteRawSamplesAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        await _writer.WaitAsync(cancellationToken);
        try
        {
            EnsureSchemaCore();
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM raw_samples WHERE record_id = $record_id;";
            cmd.Parameters.Add(new DuckDBParameter("record_id", recordId));
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writer.Release();
        }
    }

    public async Task DeleteFilteredAndFiducialsAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        await _writer.WaitAsync(cancellationToken);
        try
        {
            EnsureSchemaCore();
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText =
                """
                DELETE FROM filtered_samples WHERE record_id = $record_id;
                DELETE FROM fiducials WHERE record_id = $record_id;
                """;
            cmd.Parameters.Add(new DuckDBParameter("record_id", recordId));
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writer.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _connection.Dispose();
        _writer.Dispose();
        return ValueTask.CompletedTask;
    }

    private void EnsureSchemaCore()
    {
        if (_schemaReady)
        {
            return;
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS raw_samples (
              record_id UUID NOT NULL,
              offset_ms BIGINT NOT NULL,
              ts TIMESTAMP NOT NULL,
              value SMALLINT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS filtered_samples (
              record_id UUID NOT NULL,
              offset_ms BIGINT NOT NULL,
              value REAL NOT NULL
            );
            CREATE TABLE IF NOT EXISTS fiducials (
              record_id UUID NOT NULL,
              cycle_idx INTEGER NOT NULL,
              segment_idx INTEGER NOT NULL,
              r_ms BIGINT NOT NULL,
              q_on_ms BIGINT,
              s_off_ms BIGINT,
              p_on_ms BIGINT,
              p_off_ms BIGINT,
              t_off_ms BIGINT,
              j_ms BIGINT,
              baseline_uv REAL,
              r_amp_uv REAL
            );
            CREATE INDEX IF NOT EXISTS ix_raw_samples_record_offset ON raw_samples(record_id, offset_ms);
            CREATE INDEX IF NOT EXISTS ix_filtered_samples_record_offset ON filtered_samples(record_id, offset_ms);
            CREATE INDEX IF NOT EXISTS ix_fiducials_record ON fiducials(record_id);
            """;
        cmd.ExecuteNonQuery();
        _schemaReady = true;
    }
}
