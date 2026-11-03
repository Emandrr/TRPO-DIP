namespace SaEkg.Domain.Entities;

public class EcgRecord
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid DeviceId { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public string OriginalFilename { get; set; } = string.Empty;
    public long SampleCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FailureCode { get; set; }
    public DateTimeOffset RawPurgeAt { get; set; }
    public bool RawPurged { get; set; }
    public DateTimeOffset? FilteredPurgeAt { get; set; }
    public bool FilteredPurged { get; set; }
    public decimal UnusableRatio { get; set; }

    public Patient Patient { get; set; } = null!;
    public Device Device { get; set; } = null!;
    public AnalysisReport? AnalysisReport { get; set; }
    public ICollection<EcgGap> Gaps { get; set; } = new List<EcgGap>();
    public ICollection<Diagnosis> Diagnoses { get; set; } = new List<Diagnosis>();
}
