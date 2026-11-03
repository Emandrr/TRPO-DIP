namespace SaEkg.Domain.Entities;

public class Doctor
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;
    public ICollection<DoctorPatient> PatientLinks { get; set; } = new List<DoctorPatient>();
    public ICollection<Diagnosis> Diagnoses { get; set; } = new List<Diagnosis>();
}
