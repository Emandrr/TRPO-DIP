using System.Text;
using FluentAssertions;
using SaEkg.Application.Csv;
using Xunit;

namespace SaEkg.Tests.Unit.Csv;

public class CsvValidatorTests
{
    private readonly CsvValidator _validator = new();

    [Fact]
    public void Accepts_valid_2500_samples()
    {
        using var stream = CsvTestData.Build(2500);
        var result = _validator.Validate(stream, "ok.csv");
        result.Ok.Should().BeTrue();
        result.SampleCount.Should().Be(2500);
    }

    [Fact]
    public void Accepts_reordered_header()
    {
        var sb = new StringBuilder("errors,value,timestamp\n");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, 0);
        for (var i = 0; i < 2500; i++)
        {
            var ts = start.AddMilliseconds(i * 4);
            sb.Append($"NoError,1,{ts:yyyy-MM-dd HH:mm:ss.fff}\n");
        }

        using var stream = CsvTestData.FromText(sb.ToString());
        var result = _validator.Validate(stream, "ok.csv");
        result.Ok.Should().BeTrue();
    }

    [Fact]
    public void Check1_rejects_wrong_extension()
    {
        using var stream = CsvTestData.Build(2500);
        var result = _validator.Validate(stream, "ok.txt");
        result.FailedCheck.Should().Be(1);
    }

    [Fact]
    public void Check1_rejects_oversized_declared_length()
    {
        using var stream = CsvTestData.Build(2500);
        var result = _validator.Validate(stream, "ok.csv", declaredLengthBytes: 1L * 1024 * 1024 * 1024 + 1);
        result.FailedCheck.Should().Be(1);
    }

    [Fact]
    public void Check2_rejects_bom()
    {
        using var stream = CsvTestData.FromText("timestamp,value,errors\n2026-01-01 00:00:00.000,1,NoError\n", utf8Bom: true);
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(2);
    }

    [Fact]
    public void Check2_rejects_invalid_utf8()
    {
        var bytes = Encoding.UTF8.GetBytes("timestamp,value,errors\n");
        var invalid = bytes.Concat(new byte[] { 0xFF, 0xFE }).ToArray();
        using var stream = new MemoryStream(invalid);
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(2);
    }

    [Fact]
    public void Check3_rejects_extra_column()
    {
        using var stream = CsvTestData.FromText("timestamp,value,errors,extra\n2026-01-01 00:00:00.000,1,NoError,x\n");
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(3);
        result.Line.Should().Be(1);
    }

    [Fact]
    public void Check4_rejects_empty_data_line()
    {
        using var stream = CsvTestData.FromText("timestamp,value,errors\n\n2026-01-01 00:00:00.000,1,NoError\n");
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(4);
        result.Line.Should().Be(2);
    }

    [Fact]
    public void Check4_rejects_wrong_field_count()
    {
        using var stream = CsvTestData.FromText("timestamp,value,errors\n2026-01-01 00:00:00.000,1\n");
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(4);
    }

    [Theory]
    [InlineData("5001")]
    [InlineData("-5001")]
    [InlineData("1.5")]
    [InlineData("abc")]
    public void Check5_rejects_bad_values(string value)
    {
        using var stream = CsvTestData.Build(2500, valueFactory: _ => value);
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(5);
    }

    [Theory]
    [InlineData("5000")]
    [InlineData("-5000")]
    public void Check5_accepts_boundaries(string value)
    {
        using var stream = CsvTestData.Build(2500, valueFactory: _ => value);
        var result = _validator.Validate(stream, "ok.csv");
        result.Ok.Should().BeTrue();
    }

    [Fact]
    public void Check6_rejects_invalid_calendar_date()
    {
        var text = "timestamp,value,errors\n2026-02-30 00:00:00.000,1,NoError\n";
        using var stream = CsvTestData.FromText(text);
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(6);
    }

    [Fact]
    public void Check7_rejects_lowercase_error()
    {
        var sb = new StringBuilder("timestamp,value,errors\n");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, 0);
        for (var i = 0; i < 2500; i++)
        {
            var ts = start.AddMilliseconds(i * 4);
            sb.Append($"{ts:yyyy-MM-dd HH:mm:ss.fff},1,error\n");
        }

        using var stream = CsvTestData.FromText(sb.ToString());
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(7);
    }

    [Fact]
    public void Check8_rejects_time_going_backwards()
    {
        var text =
            "timestamp,value,errors\n" +
            "2026-01-01 00:00:00.004,1,NoError\n" +
            "2026-01-01 00:00:00.000,1,NoError\n";
        using var stream = CsvTestData.FromText(text);
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(8);
    }

    [Fact]
    public void Check9_rejects_same_timestamp_different_value()
    {
        var text =
            "timestamp,value,errors\n" +
            "2026-01-01 00:00:00.000,1,NoError\n" +
            "2026-01-01 00:00:00.000,2,NoError\n";
        using var stream = CsvTestData.FromText(text);
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(9);
    }

    [Fact]
    public void Identical_duplicates_are_skipped()
    {
        var sb = new StringBuilder("timestamp,value,errors\n");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, 0);
        for (var i = 0; i < 2500; i++)
        {
            var ts = start.AddMilliseconds(i * 4);
            sb.Append($"{ts:yyyy-MM-dd HH:mm:ss.fff},1,NoError\n");
            if (i == 10)
            {
                sb.Append($"{ts:yyyy-MM-dd HH:mm:ss.fff},1,NoError\n");
            }
        }

        using var stream = CsvTestData.FromText(sb.ToString());
        var result = _validator.Validate(stream, "ok.csv");
        result.Ok.Should().BeTrue();
        result.SampleCount.Should().Be(2500);
    }

    [Fact]
    public void Check10_rejects_wrong_median_interval()
    {
        using var stream = CsvTestData.Build(2500, intervalMs: 8);
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(10);
    }

    [Fact]
    public void Check11_rejects_2499_samples_under_10_seconds()
    {
        using var stream = CsvTestData.Build(2499);
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(11);
    }

    [Fact]
    public void Check12_rejects_unusable_ratio_above_30_percent()
    {
        var sb = new StringBuilder("timestamp,value,errors\n");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, 0);
        for (var i = 0; i < 2500; i++)
        {
            var ts = start.AddMilliseconds(i * 4);
            var errors = i < 800 ? "Error" : "NoError";
            sb.Append($"{ts:yyyy-MM-dd HH:mm:ss.fff},1,{errors}\n");
        }

        using var stream = CsvTestData.FromText(sb.ToString());
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(12);
    }

    [Fact]
    public void Check12_accepts_unusable_ratio_29_percent()
    {
        var sb = new StringBuilder("timestamp,value,errors\n");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, 0);
        for (var i = 0; i < 2500; i++)
        {
            var ts = start.AddMilliseconds(i * 4);
            var errors = i < 725 ? "Error" : "NoError";
            sb.Append($"{ts:yyyy-MM-dd HH:mm:ss.fff},1,{errors}\n");
        }

        using var stream = CsvTestData.FromText(sb.ToString());
        var result = _validator.Validate(stream, "ok.csv");
        result.Ok.Should().BeTrue();
        result.Gaps.Should().NotBeEmpty();
    }

    [Fact]
    public void Accepts_crlf_line_endings()
    {
        using var stream = CsvTestData.Build(2500, lineEnding: "\r\n");
        var result = _validator.Validate(stream, "ok.csv");
        result.Ok.Should().BeTrue();
    }

    [Fact]
    public void Gap_is_recorded_when_interval_greater_than_4ms()
    {
        using var stream = CsvTestData.Build(2500, gapAfterIndex: 100, gapMs: 20);
        var result = _validator.Validate(stream, "ok.csv");
        result.Ok.Should().BeTrue();
        result.Gaps.Should().Contain(g => g.Reason == "Gap");
    }

    [Fact]
    public void Accepts_lf_line_endings()
    {
        using var stream = CsvTestData.Build(2500, lineEnding: "\n");
        _validator.Validate(stream, "ok.csv").Ok.Should().BeTrue();
    }

    [Theory]
    [InlineData("timestamp,VALUE,errors")]
    [InlineData("Timestamp,value,Errors")]
    public void Check3_rejects_non_lowercase_header(string header)
    {
        using var stream = CsvTestData.FromText(header + "\n2026-01-01 00:00:00.000,1,NoError\n");
        _validator.Validate(stream, "ok.csv").FailedCheck.Should().Be(3);
    }

    [Fact]
    public void Check3_rejects_missing_column()
    {
        using var stream = CsvTestData.FromText("timestamp,value\n2026-01-01 00:00:00.000,1\n");
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(3);
        result.Line.Should().Be(1);
    }

    [Theory]
    [InlineData("2026-01-01 00:00:00")]
    [InlineData("2026/01/01 00:00:00.000")]
    [InlineData("01-01-2026 00:00:00.000")]
    [InlineData("2026-13-01 00:00:00.000")]
    public void Check6_rejects_bad_timestamp_format(string ts)
    {
        using var stream = CsvTestData.FromText($"timestamp,value,errors\n{ts},1,NoError\n");
        _validator.Validate(stream, "ok.csv").FailedCheck.Should().Be(6);
    }

    [Theory]
    [InlineData("ERROR")]
    [InlineData("noerror")]
    [InlineData("No Err")]
    [InlineData("")]
    public void Check7_rejects_invalid_errors_tokens(string errors)
    {
        var sb = new StringBuilder("timestamp,value,errors\n");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, 0);
        for (var i = 0; i < 2500; i++)
        {
            sb.Append($"{start.AddMilliseconds(i * 4):yyyy-MM-dd HH:mm:ss.fff},1,{errors}\n");
        }

        using var stream = CsvTestData.FromText(sb.ToString());
        _validator.Validate(stream, "ok.csv").FailedCheck.Should().Be(7);
    }

    [Fact]
    public void Check9_rejects_same_timestamp_different_errors()
    {
        var text =
            "timestamp,value,errors\n" +
            "2026-01-01 00:00:00.000,1,NoError\n" +
            "2026-01-01 00:00:00.000,1,Error\n";
        using var stream = CsvTestData.FromText(text);
        _validator.Validate(stream, "ok.csv").FailedCheck.Should().Be(9);
    }

    [Fact]
    public void Check12_boundary_exactly_30_percent_accepted()
    {
        var sb = new StringBuilder("timestamp,value,errors\n");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, 0);
        for (var i = 0; i < 2500; i++)
        {
            var errors = i < 750 ? "Error" : "NoError";
            sb.Append($"{start.AddMilliseconds(i * 4):yyyy-MM-dd HH:mm:ss.fff},1,{errors}\n");
        }

        using var stream = CsvTestData.FromText(sb.ToString());
        _validator.Validate(stream, "ok.csv").Ok.Should().BeTrue();
    }

    [Fact]
    public void Check12_boundary_3001_percent_rejected()
    {
        var sb = new StringBuilder("timestamp,value,errors\n");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, 0);
        for (var i = 0; i < 2500; i++)
        {
            var errors = i < 751 ? "Error" : "NoError";
            sb.Append($"{start.AddMilliseconds(i * 4):yyyy-MM-dd HH:mm:ss.fff},1,{errors}\n");
        }

        using var stream = CsvTestData.FromText(sb.ToString());
        _validator.Validate(stream, "ok.csv").FailedCheck.Should().Be(12);
    }

    [Fact]
    public void Check11_accepts_exactly_2500_samples_10_seconds()
    {
        using var stream = CsvTestData.Build(2500);
        var result = _validator.Validate(stream, "ok.csv");
        result.Ok.Should().BeTrue();
        result.SampleCount.Should().Be(2500);
    }

    [Fact]
    public void Check5_reports_line_number_for_bad_value()
    {
        using var stream = CsvTestData.Build(2500, valueFactory: i => i == 5 ? "5001" : "1");
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(5);
        result.Line.Should().Be(7);
    }

    [Fact]
    public void Check4_rejects_quoted_fields()
    {
        using var stream = CsvTestData.FromText("timestamp,value,errors\n\"2026-01-01 00:00:00.000\",1,NoError\n");
        _validator.Validate(stream, "ok.csv").FailedCheck.Should().Be(4);
    }

    [Fact]
    public void Check1_rejects_missing_filename_extension()
    {
        using var stream = CsvTestData.Build(2500);
        _validator.Validate(stream, "ok").FailedCheck.Should().Be(1);
    }

    [Theory]
    [InlineData(".CSV")]
    [InlineData(".Csv")]
    [InlineData("file.CSV")]
    public void Check1_accepts_csv_extension_case_insensitive(string name)
    {
        using var stream = CsvTestData.Build(2500);
        _validator.Validate(stream, name.StartsWith('.') ? "file" + name : name).Ok.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(2498)]
    public void Check11_rejects_short_record_sample_counts(int samples)
    {
        using var stream = CsvTestData.Build(samples);
        _validator.Validate(stream, "ok.csv").FailedCheck.Should().Be(11);
    }

    [Theory]
    [InlineData("2026-01-01 24:00:00.000")]
    [InlineData("2026-01-01 00:60:00.000")]
    [InlineData("2026-01-01 00:00:60.000")]
    [InlineData("2026-04-31 00:00:00.000")]
    public void Check6_rejects_impossible_clock_values(string ts)
    {
        using var stream = CsvTestData.FromText($"timestamp,value,errors\n{ts},1,NoError\n");
        _validator.Validate(stream, "ok.csv").FailedCheck.Should().Be(6);
    }

    [Fact]
    public void Check8_allows_equal_timestamp_when_identical_duplicate()
    {
        var sb = new StringBuilder("timestamp,value,errors\n");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, 0);
        for (var i = 0; i < 2500; i++)
        {
            var ts = start.AddMilliseconds(i * 4);
            sb.Append($"{ts:yyyy-MM-dd HH:mm:ss.fff},1,NoError\n");
            if (i == 50)
            {
                sb.Append($"{ts:yyyy-MM-dd HH:mm:ss.fff},1,NoError\n");
            }
        }

        using var stream = CsvTestData.FromText(sb.ToString());
        _validator.Validate(stream, "ok.csv").Ok.Should().BeTrue();
    }

    [Fact]
    public void Check10_accepts_median_4ms_with_sparse_gaps()
    {
        using var stream = CsvTestData.Build(2500, gapAfterIndex: 10, gapMs: 40);
        var result = _validator.Validate(stream, "ok.csv");
        result.Ok.Should().BeTrue();
        result.Gaps.Should().NotBeEmpty();
    }

    [Fact]
    public void Check5_accepts_zero_value()
    {
        using var stream = CsvTestData.Build(2500, valueFactory: _ => "0");
        _validator.Validate(stream, "ok.csv").Ok.Should().BeTrue();
    }

    [Theory]
    [InlineData("+1")]
    [InlineData("1e2")]
    [InlineData(" ")]
    [InlineData("--1")]
    public void Check5_rejects_non_canonical_integers(string value)
    {
        using var stream = CsvTestData.Build(2500, valueFactory: _ => value);
        _validator.Validate(stream, "ok.csv").FailedCheck.Should().Be(5);
    }

    [Fact]
    public void Global_checks_omit_line_for_check10()
    {
        using var stream = CsvTestData.Build(2500, intervalMs: 8);
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(10);
        result.Line.Should().BeNull();
    }

    [Fact]
    public void Global_checks_omit_line_for_check12()
    {
        var sb = new StringBuilder("timestamp,value,errors\n");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, 0);
        for (var i = 0; i < 2500; i++)
        {
            sb.Append($"{start.AddMilliseconds(i * 4):yyyy-MM-dd HH:mm:ss.fff},1,{(i < 800 ? "Error" : "NoError")}\n");
        }

        using var stream = CsvTestData.FromText(sb.ToString());
        var result = _validator.Validate(stream, "ok.csv");
        result.FailedCheck.Should().Be(12);
        result.Line.Should().BeNull();
    }
}
