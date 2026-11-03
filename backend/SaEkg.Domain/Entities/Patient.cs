namespace SaEkg.Domain.Entities;

public class Patient
{
    public Guid UserId { get; set; }
    public bool ConsentSigned { get; set; }
    public DateTimeOffset? ConsentSignedAt { get; set; }

    public User User { get; set; } = null!;
    public MedicalCard? MedicalCard { get; set; }
    public ICollection<DoctorPatient> DoctorLinks { get; set; } = new List<DoctorPatient>();
    public ICollection<EcgRecord> EcgRecords { get; set; } = new List<EcgRecord>();
    public ICollection<Diagnosis> Diagnoses { get; set; } = new List<Diagnosis>();
}
