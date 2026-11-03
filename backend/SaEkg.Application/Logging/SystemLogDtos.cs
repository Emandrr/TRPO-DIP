namespace SaEkg.Application.Logging;

public sealed record SystemLogDto(
    long Id,
    DateTimeOffset Ts,
    string Level,
    string Module,
    string? Code,
    string Message,
    Guid? UserId,
    string? RequestId);

public sealed record SystemLogQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Level,
    string? Module,
    string? Code,
    int Page,
    int PageSize);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total);
