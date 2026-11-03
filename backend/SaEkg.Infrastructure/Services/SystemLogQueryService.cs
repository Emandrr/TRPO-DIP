using Microsoft.EntityFrameworkCore;
using SaEkg.Application.Logging;
using SaEkg.Infrastructure.Persistence;

namespace SaEkg.Infrastructure.Services;

public sealed class SystemLogQueryService(SaEkgDbContext db) : ISystemLogQueryService
{
    public async Task<PagedResult<SystemLogDto>> QueryAsync(SystemLogQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 50 : query.PageSize;

        var q = db.SystemLogs.AsNoTracking().AsQueryable();
        if (query.From is not null)
        {
            q = q.Where(x => x.Ts >= query.From);
        }

        if (query.To is not null)
        {
            q = q.Where(x => x.Ts <= query.To);
        }

        if (!string.IsNullOrWhiteSpace(query.Level))
        {
            q = q.Where(x => x.Level == query.Level);
        }

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            q = q.Where(x => x.Module == query.Module);
        }

        if (!string.IsNullOrWhiteSpace(query.Code))
        {
            q = q.Where(x => x.Code == query.Code);
        }

        var total = await q.LongCountAsync(cancellationToken);
        var items = await q
            .OrderByDescending(x => x.Ts)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SystemLogDto(x.Id, x.Ts, x.Level, x.Module, x.Code, x.Message, x.UserId, x.RequestId))
            .ToListAsync(cancellationToken);

        return new PagedResult<SystemLogDto>(items, page, pageSize, total);
    }
}
