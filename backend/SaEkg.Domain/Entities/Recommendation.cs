namespace SaEkg.Domain.Entities;

public class Recommendation
{
    public Guid Id { get; set; }
    public Guid DiagnosisId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Diagnosis Diagnosis { get; set; } = null!;
}
