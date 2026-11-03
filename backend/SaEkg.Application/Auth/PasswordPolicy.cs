using System.Text.RegularExpressions;
using SaEkg.Domain.Constants;

namespace SaEkg.Application.Auth;

public static partial class PasswordPolicy
{
    public static bool IsValid(string? password, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(password) || password.Length < AuthConstants.MinPasswordLength)
        {
            error = $"Пароль должен содержать не менее {AuthConstants.MinPasswordLength} символов.";
            return false;
        }

        if (!HasLetter().IsMatch(password) || !HasDigit().IsMatch(password))
        {
            error = "Пароль должен содержать хотя бы одну букву и одну цифру.";
            return false;
        }

        return true;
    }

    [GeneratedRegex(@"\p{L}")]
    private static partial Regex HasLetter();

    [GeneratedRegex(@"\d")]
    private static partial Regex HasDigit();
}
