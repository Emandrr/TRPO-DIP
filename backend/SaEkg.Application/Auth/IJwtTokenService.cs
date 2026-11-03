using System.Security.Claims;
using SaEkg.Domain.Entities;

namespace SaEkg.Application.Auth;

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user);
    string CreateRefreshToken();
    string HashRefreshToken(string refreshToken);
    ClaimsPrincipal? ValidateAccessToken(string token, bool validateLifetime);
}
