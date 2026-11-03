using System.Globalization;
using SaEkg.Domain.Enums;

namespace SaEkg.Application.Medical;

public static class MedDataValidators
{
    public static bool TryParseRuDate(string? input, out DateOnly date)
    {
        date = default;
        return !string.IsNullOrWhiteSpace(input)
               && DateOnly.TryParseExact(input.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    public static string? ValidateRequiredLine(string? value, string field)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > 50)
        {
            return $"{field}: обязательное поле, длина 1…50.";
        }

        return null;
    }

    public static string? ValidateOptionalMultiline(string? value, string field)
    {
        if (value is null)
        {
            return null;
        }

        return value.Length > 5000 ? $"{field}: не более 5000 символов." : null;
    }

    public static string? ValidateBirthDate(string? input)
    {
        if (!TryParseRuDate(input, out var date))
        {
            return "birthDate: ожидается дата в формате DD.MM.YYYY.";
        }

        return date > DateOnly.FromDateTime(DateTime.UtcNow.Date)
            ? "birthDate: не может быть в будущем."
            : null;
    }

    public static string? ValidateDiagnosisDate(string? input)
    {
        if (!TryParseRuDate(input, out var date))
        {
            return "diagnosedOn: ожидается дата в формате DD.MM.YYYY.";
        }

        return date > DateOnly.FromDateTime(DateTime.UtcNow.Date)
            ? "diagnosedOn: не может быть в будущем."
            : null;
    }

    public static string? ValidateSexRu(string? input)
    {
        return input?.Trim() switch
        {
            "мужской" or "женский" => null,
            _ => "sex: допустимы значения «мужской» или «женский»."
        };
    }

    public static string MapSexToDb(string sexRu) => sexRu.Trim() switch
    {
        "мужской" => Sex.Male,
        "женский" => Sex.Female,
        _ => throw new ArgumentOutOfRangeException(nameof(sexRu))
    };

    public static string MapSexToRu(string sexDb) => sexDb switch
    {
        Sex.Male => "мужской",
        Sex.Female => "женский",
        _ => sexDb
    };

    public static string? ValidatePrescriptionDates(string? start, string? end)
    {
        if (!TryParseRuDate(start, out var startDate))
        {
            return "startDate: ожидается дата в формате DD.MM.YYYY.";
        }

        if (!TryParseRuDate(end, out var endDate))
        {
            return "endDate: ожидается дата в формате DD.MM.YYYY.";
        }

        return endDate < startDate ? "endDate: должна быть не раньше startDate." : null;
    }

    public static List<string> Collect(params string?[] errors) =>
        errors.Where(e => e is not null).Select(e => e!).ToList();
}

public sealed class MedValidationException : Exception
{
    public MedValidationException(IReadOnlyList<string> errors)
        : base("Ошибка валидации медицинских данных.")
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
