namespace SaEkg.Domain.Entities;

public class BackgroundJob
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTimeOffset RunAfter { get; set; }
    public string? LockedBy { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public string? LastError { get; set; }
}
