namespace SaEkg.Domain.Entities;

public class DoctorPatient
{
    public Guid DoctorId { get; set; }
    public Guid PatientId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public Guid AssignedBy { get; set; }

    public Doctor Doctor { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}
