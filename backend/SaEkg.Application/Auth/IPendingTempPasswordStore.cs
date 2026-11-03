namespace SaEkg.Application.Auth;

public interface IPendingTempPasswordStore
{
    void Store(Guid userId, string email, string temporaryPassword);
    bool TryTake(Guid userId, out PendingTempPassword? pending);
}

public sealed record PendingTempPassword(string Email, string TemporaryPassword, DateTimeOffset CreatedAt);
