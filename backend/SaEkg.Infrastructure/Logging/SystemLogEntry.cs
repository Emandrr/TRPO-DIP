namespace SaEkg.Infrastructure.Logging;

public sealed class SystemLogEntry
{
    public required DateTimeOffset Ts { get; init; }
    public required string Level { get; init; }
    public required string Module { get; init; }
    public string? Code { get; init; }
    public required string Message { get; init; }
    public Guid? UserId { get; init; }
    public string? RequestId { get; init; }
    public string? DetailsJson { get; init; }
}
