namespace SaEkg.Application.Auth;

public sealed class AuthException : Exception
{
    public AuthException(string code, string message, object? details = null)
        : base(message)
    {
        Code = code;
        Details = details;
    }

    public string Code { get; }
    public object? Details { get; }
}
