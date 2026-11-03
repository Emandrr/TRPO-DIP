using System.Collections.Concurrent;
using SaEkg.Application.Auth;

namespace SaEkg.Infrastructure.Auth;

public sealed class PendingTempPasswordStore : IPendingTempPasswordStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<Guid, PendingTempPassword> _items = new();

    public void Store(Guid userId, string email, string temporaryPassword)
    {
        _items[userId] = new PendingTempPassword(email, temporaryPassword, DateTimeOffset.UtcNow);
    }

    public bool TryTake(Guid userId, out PendingTempPassword? pending)
    {
        pending = null;
        if (!_items.TryRemove(userId, out var item))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow - item.CreatedAt > Ttl)
        {
            return false;
        }

        pending = item;
        return true;
    }
}
