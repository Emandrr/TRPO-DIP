namespace SaEkg.Domain.Entities;

public class MedicalCard
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string? LastName { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public DateOnly BirthDate { get; set; }
    public string Sex { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public Patient Patient { get; set; } = null!;
}
