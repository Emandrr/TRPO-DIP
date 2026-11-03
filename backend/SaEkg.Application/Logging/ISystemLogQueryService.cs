namespace SaEkg.Application.Logging;

public interface ISystemLogQueryService
{
    Task<PagedResult<SystemLogDto>> QueryAsync(SystemLogQuery query, CancellationToken cancellationToken = default);
}
