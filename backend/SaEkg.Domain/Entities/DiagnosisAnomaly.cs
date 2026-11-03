namespace SaEkg.Domain.Entities;

public class DiagnosisAnomaly
{
    public Guid DiagnosisId { get; set; }
    public Guid AnomalyId { get; set; }

    public Diagnosis Diagnosis { get; set; } = null!;
    public Anomaly Anomaly { get; set; } = null!;
}
