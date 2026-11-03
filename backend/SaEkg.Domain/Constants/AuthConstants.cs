namespace SaEkg.Domain.Constants;

public static class AuthConstants
{
    public const int AccessTokenMinutes = 15;
    public const int RefreshTokenDays = 7;
    public const int MaxFailedLogins = 5;
    public const int LockoutMinutes = 15;
    public const int MinPasswordLength = 8;
    public const int TempPasswordLength = 12;
}
