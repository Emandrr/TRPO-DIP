namespace SaEkg.Domain.Entities;

public class Prescription
{
    public Guid Id { get; set; }
    public Guid DiagnosisId { get; set; }
    public string DrugName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Comment { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Diagnosis Diagnosis { get; set; } = null!;
}
