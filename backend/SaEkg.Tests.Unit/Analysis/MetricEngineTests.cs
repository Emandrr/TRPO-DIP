using FluentAssertions;
using SaEkg.Application.Analysis;
using SaEkg.Domain.Enums;
using Xunit;

namespace SaEkg.Tests.Unit.Analysis;

public class MetricEngineTests
{
    [Theory]
    [InlineData(400, 1000, 400)]
    [InlineData(450, 1000, 450)]
    public void Qtc_bazett(double qt, double rr, double expected)
    {
        MetricEngine.QtcBazett(qt, rr).Should().BeApproximately(expected, 0.01);
    }

    [Fact]
    public void Detects_tachycardia_in_window()
    {
        var r = new List<long>();
        long t = 0;
        for (var i = 0; i < 40; i++)
        {
            r.Add(t);
            t += 500;
        }

        var (_, anomalies) = MetricEngine.Analyze(r, Array.Empty<FiducialPoints>(), Sex.Male);
        anomalies.Should().Contain(a => a.IndicatorCode == "HR" && a.Direction == AnomalyDirection.High);
    }

    [Fact]
    public void Detects_bradycardia_in_window()
    {
        var r = new List<long>();
        long t = 0;
        for (var i = 0; i < 20; i++)
        {
            r.Add(t);
            t += 1200;
        }

        var (_, anomalies) = MetricEngine.Analyze(r, Array.Empty<FiducialPoints>(), Sex.Female);
        anomalies.Should().Contain(a => a.IndicatorCode == "HR" && a.Direction == AnomalyDirection.Low);
    }

    [Theory]
    [InlineData(599)]
    [InlineData(1001)]
    public void Rr_boundaries_are_anomalies(double rr)
    {
        var r = new List<long> { 0, (long)rr, (long)(rr * 2) };
        var fid = new List<FiducialPoints>
        {
            new(0, 0, 80, null, null, null, null, 0, 1)
        };
        var (_, anomalies) = MetricEngine.Analyze(r, fid, Sex.Male);
        anomalies.Should().Contain(a => a.IndicatorCode == "RR");
    }

    [Fact]
    public void Merge_joins_close_anomalies()
    {
        var input = new[]
        {
            new DetectedAnomaly("HR", "High", "Тахикардия", 0, 1000, 120, 120, "уд/мин", "60–100", 1),
            new DetectedAnomaly("HR", "High", "Тахикардия", 3000, 4000, 130, 130, "уд/мин", "60–100", 1)
        };
        var merged = AnomalyMerger.Merge(input);
        merged.Should().HaveCount(1);
        merged[0].ValueMin.Should().Be(120);
        merged[0].ValueMax.Should().Be(130);
        merged[0].CycleCount.Should().Be(2);
    }
}
