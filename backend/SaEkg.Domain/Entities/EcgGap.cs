namespace SaEkg.Domain.Entities;

public class EcgGap
{
    public Guid Id { get; set; }
    public Guid RecordId { get; set; }
    public long StartOffsetMs { get; set; }
    public long EndOffsetMs { get; set; }
    public string Reason { get; set; } = string.Empty;

    public EcgRecord Record { get; set; } = null!;
}
