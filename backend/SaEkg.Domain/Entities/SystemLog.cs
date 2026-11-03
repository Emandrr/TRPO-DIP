namespace SaEkg.Domain.Entities;

public class SystemLog
{
    public long Id { get; set; }
    public DateTimeOffset Ts { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? RequestId { get; set; }
    public string? DetailsJson { get; set; }
}
