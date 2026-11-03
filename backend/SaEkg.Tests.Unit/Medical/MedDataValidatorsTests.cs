using FluentAssertions;
using SaEkg.Application.Medical;
using SaEkg.Domain.Enums;
using Xunit;

namespace SaEkg.Tests.Unit.Medical;

public class MedDataValidatorsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("012345678901234567890123456789012345678901234567891")]
    public void Required_line_rejects_invalid(string? value)
    {
        MedDataValidators.ValidateRequiredLine(value, "title").Should().NotBeNull();
    }

    [Theory]
    [InlineData("А")]
    [InlineData("Синусовая тахикардия")]
    [InlineData("01234567890123456789012345678901234567890123456789")]
    public void Required_line_accepts_valid(string value)
    {
        MedDataValidators.ValidateRequiredLine(value, "title").Should().BeNull();
    }

    [Fact]
    public void Optional_multiline_allows_null_and_rejects_too_long()
    {
        MedDataValidators.ValidateOptionalMultiline(null, "comment").Should().BeNull();
        MedDataValidators.ValidateOptionalMultiline(new string('x', 5000), "comment").Should().BeNull();
        MedDataValidators.ValidateOptionalMultiline(new string('x', 5001), "comment").Should().NotBeNull();
    }

    [Theory]
    [InlineData("32.13.2020")]
    [InlineData("2020-01-01")]
    [InlineData("01/01/2020")]
    [InlineData("")]
    [InlineData(null)]
    public void Birth_date_rejects_bad_format(string? value)
    {
        MedDataValidators.ValidateBirthDate(value).Should().NotBeNull();
    }

    [Fact]
    public void Birth_date_rejects_future()
    {
        var future = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)).ToString("dd.MM.yyyy");
        MedDataValidators.ValidateBirthDate(future).Should().Contain("будущем");
    }

    [Fact]
    public void Birth_date_accepts_today_and_past()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date).ToString("dd.MM.yyyy");
        MedDataValidators.ValidateBirthDate(today).Should().BeNull();
        MedDataValidators.ValidateBirthDate("15.03.1990").Should().BeNull();
    }

    [Fact]
    public void Diagnosis_date_rejects_future_accepts_today()
    {
        var future = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2)).ToString("dd.MM.yyyy");
        MedDataValidators.ValidateDiagnosisDate(future).Should().Contain("будущем");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date).ToString("dd.MM.yyyy");
        MedDataValidators.ValidateDiagnosisDate(today).Should().BeNull();
    }

    [Theory]
    [InlineData("мужской")]
    [InlineData("женский")]
    public void Sex_accepts_ru(string sex)
    {
        MedDataValidators.ValidateSexRu(sex).Should().BeNull();
    }

    [Theory]
    [InlineData("male")]
    [InlineData("м")]
    [InlineData("")]
    [InlineData(null)]
    public void Sex_rejects_other(string? sex)
    {
        MedDataValidators.ValidateSexRu(sex).Should().NotBeNull();
    }

    [Fact]
    public void Sex_maps_ru_to_db_and_back()
    {
        MedDataValidators.MapSexToDb("мужской").Should().Be(Sex.Male);
        MedDataValidators.MapSexToDb("женский").Should().Be(Sex.Female);
        MedDataValidators.MapSexToRu(Sex.Male).Should().Be("мужской");
        MedDataValidators.MapSexToRu(Sex.Female).Should().Be("женский");
    }

    [Fact]
    public void Prescription_dates_allow_future_start_and_require_end_ge_start()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(10)).ToString("dd.MM.yyyy");
        var end = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(20)).ToString("dd.MM.yyyy");
        MedDataValidators.ValidatePrescriptionDates(start, end).Should().BeNull();
        MedDataValidators.ValidatePrescriptionDates(end, start).Should().Contain("endDate");
        MedDataValidators.ValidatePrescriptionDates("01.01.2020", "01.01.2020").Should().BeNull();
    }

    [Fact]
    public void Collect_filters_nulls()
    {
        var errors = MedDataValidators.Collect(null, "a", null, "b");
        errors.Should().Equal("a", "b");
    }
}
