namespace SaEkg.Domain.Entities;

public class AuditAccess
{
    public Guid Id { get; set; }
    public DateTimeOffset Ts { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
}
