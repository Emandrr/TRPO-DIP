namespace SaEkg.Domain.Entities;

public class Diagnosis
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid? RecordId { get; set; }
    public Guid? ReportId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly DiagnosedOn { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor Doctor { get; set; } = null!;
    public EcgRecord? Record { get; set; }
    public AnalysisReport? Report { get; set; }
    public ICollection<DiagnosisAnomaly> AnomalyLinks { get; set; } = new List<DiagnosisAnomaly>();
    public ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
}
