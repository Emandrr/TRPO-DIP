using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaEkg.Application.Auth;
using SaEkg.Domain.Constants;
using SaEkg.Domain.Entities;
using SaEkg.Domain.Enums;
using SaEkg.Infrastructure.Persistence;

namespace SaEkg.Infrastructure.Auth;

public sealed class AuthService(
    SaEkgDbContext db,
    IJwtTokenService tokens,
    PasswordHasher<User> passwordHasher) : IAuthService
{
    public async Task<TokenResponse> RegisterPatientAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new AuthException("VALIDATION_ERROR", "Укажите корректный email.");
        }

        if (!PasswordPolicy.IsValid(request.Password, out var passwordError))
        {
            throw new AuthException("VALIDATION_ERROR", passwordError!);
        }

        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            throw new AuthException("CONFLICT", "Пользователь с таким email уже существует.");
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Role = UserRole.Patient,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        db.Patients.Add(new Patient { UserId = user.Id });
        await db.SaveChangesAsync(cancellationToken);
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null)
        {
            throw new AuthException("UNAUTHORIZED", "Неверный email или пароль.");
        }

        if (user.IsBlocked)
        {
            throw new AuthException("FORBIDDEN", "Учётная запись заблокирована.");
        }

        if (user.LockedUntil is not null && user.LockedUntil > DateTimeOffset.UtcNow)
        {
            throw new AuthException("ACCOUNT_LOCKED", "Учётная запись временно заблокирована из‑за неудачных попыток входа.");
        }

        var verify = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount += 1;
            if (user.FailedLoginCount >= AuthConstants.MaxFailedLogins)
            {
                user.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(AuthConstants.LockoutMinutes);
                user.FailedLoginCount = 0;
            }

            user.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw new AuthException(
                user.LockedUntil is not null && user.LockedUntil > DateTimeOffset.UtcNow
                    ? "ACCOUNT_LOCKED"
                    : "UNAUTHORIZED",
                user.LockedUntil is not null && user.LockedUntil > DateTimeOffset.UtcNow
                    ? "Учётная запись временно заблокирована из‑за неудачных попыток входа."
                    : "Неверный email или пароль.");
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new AuthException("UNAUTHORIZED", "Недействительный refresh-токен.");
        }

        var hash = tokens.HashRefreshToken(request.RefreshToken);
        var existing = await db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (existing is null)
        {
            throw new AuthException("UNAUTHORIZED", "Недействительный refresh-токен.");
        }

        if (existing.RevokedAt is not null)
        {
            await RevokeAllRefreshTokensAsync(existing.UserId, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            throw new AuthException("UNAUTHORIZED", "Refresh-токен уже был использован. Все сессии отозваны.");
        }

        if (existing.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new AuthException("UNAUTHORIZED", "Срок действия refresh-токена истёк.");
        }

        if (existing.User.IsBlocked)
        {
            throw new AuthException("FORBIDDEN", "Учётная запись заблокирована.");
        }

        var response = await IssueTokensAsync(existing.User, cancellationToken);
        existing.RevokedAt = DateTimeOffset.UtcNow;
        var newHash = tokens.HashRefreshToken(response.RefreshToken);
        var replacement = await db.RefreshTokens.FirstAsync(x => x.TokenHash == newHash, cancellationToken);
        existing.ReplacedBy = replacement.Id;
        await db.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var hash = tokens.HashRefreshToken(request.RefreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (existing is null || existing.RevokedAt is not null)
        {
            return;
        }

        existing.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (!PasswordPolicy.IsValid(request.NewPassword, out var passwordError))
        {
            throw new AuthException("VALIDATION_ERROR", passwordError!);
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new AuthException("NOT_FOUND", "Пользователь не найден.");

        var verify = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verify == PasswordVerificationResult.Failed)
        {
            throw new AuthException("UNAUTHORIZED", "Текущий пароль указан неверно.");
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await RevokeAllRefreshTokensAsync(user.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<TokenResponse> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var (access, expires) = tokens.CreateAccessToken(user);
        var refresh = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokens.HashRefreshToken(refresh),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(AuthConstants.RefreshTokenDays)
        });
        await db.SaveChangesAsync(cancellationToken);
        return new TokenResponse(access, refresh, expires, user.MustChangePassword, user.Role, user.Id);
    }

    private async Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var active = await db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active)
        {
            token.RevokedAt = now;
        }
    }

    private static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}
