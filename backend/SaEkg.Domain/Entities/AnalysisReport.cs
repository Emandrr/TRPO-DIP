namespace SaEkg.Domain.Entities;

public class AnalysisReport
{
    public Guid Id { get; set; }
    public Guid RecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionComment { get; set; }
    public string SummaryJson { get; set; } = "{}";

    public EcgRecord Record { get; set; } = null!;
    public User? Decider { get; set; }
    public ICollection<Anomaly> Anomalies { get; set; } = new List<Anomaly>();
    public ICollection<Diagnosis> Diagnoses { get; set; } = new List<Diagnosis>();
}
