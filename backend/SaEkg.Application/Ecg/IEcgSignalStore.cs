namespace SaEkg.Application.Ecg;

public interface IEcgSignalStore
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    Task AppendRawSamplesAsync(
        Guid recordId,
        IReadOnlyList<RawSamplePoint> samples,
        CancellationToken cancellationToken = default);

    Task AppendFilteredSamplesAsync(
        Guid recordId,
        IReadOnlyList<FilteredSamplePoint> samples,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RawSamplePoint>> ReadRawSamplesAsync(
        Guid recordId,
        CancellationToken cancellationToken = default);

    Task AppendFiducialsAsync(
        Guid recordId,
        IReadOnlyList<FiducialSamplePoint> fiducials,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DownsampledPoint>> ReadFilteredDownsampledAsync(
        Guid recordId,
        long fromMs,
        long toMs,
        int maxPoints,
        CancellationToken cancellationToken = default);

    Task DeleteByRecordIdAsync(Guid recordId, CancellationToken cancellationToken = default);

    Task DeleteRawSamplesAsync(Guid recordId, CancellationToken cancellationToken = default);

    Task DeleteFilteredAndFiducialsAsync(Guid recordId, CancellationToken cancellationToken = default);
}

public readonly record struct RawSamplePoint(long OffsetMs, DateTime Ts, short Value);

public readonly record struct FilteredSamplePoint(long OffsetMs, float ValueUv);

public readonly record struct FiducialSamplePoint(
    int CycleIdx,
    int SegmentIdx,
    long RMs,
    long? QOnMs,
    long? SOffMs,
    long? POnMs,
    long? POffMs,
    long? TOffMs,
    long? JMs,
    float BaselineUv,
    float RAmpUv);

public readonly record struct DownsampledPoint(long OffsetMs, float MinUv, float MaxUv);
