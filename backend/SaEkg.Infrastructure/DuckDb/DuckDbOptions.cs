namespace SaEkg.Infrastructure.DuckDb;

public sealed class DuckDbOptions
{
    public const string SectionName = "DuckDb";

    public string Path { get; set; } = "data/ecg.duckdb";
}
