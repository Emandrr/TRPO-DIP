namespace SaEkg.Domain.Entities;

public class Anomaly
{
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }
    public string IndicatorCode { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public long StartOffsetMs { get; set; }
    public long EndOffsetMs { get; set; }
    public decimal? ValueMin { get; set; }
    public decimal? ValueMax { get; set; }
    public string? Unit { get; set; }
    public string? ReferenceText { get; set; }
    public int CycleCount { get; set; }

    public AnalysisReport Report { get; set; } = null!;
    public ICollection<DiagnosisAnomaly> DiagnosisLinks { get; set; } = new List<DiagnosisAnomaly>();
}
