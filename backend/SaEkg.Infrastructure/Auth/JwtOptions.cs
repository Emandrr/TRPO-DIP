namespace SaEkg.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "sa-ekg";
    public string Audience { get; set; } = "sa-ekg";
    public string SigningKey { get; set; } = "CHANGE_ME_TO_A_LONG_RANDOM_SECRET_KEY_32+";
}
