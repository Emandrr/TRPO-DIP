namespace SaEkg.Application.Ecg;

public interface IPurgeService
{
    Task<int> PurgeRawAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);
    Task<int> PurgeFilteredAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);
}
