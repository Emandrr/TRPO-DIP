using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace SaEkg.Infrastructure.Logging;

public sealed class SystemLogChannel
{
    private readonly Channel<SystemLogEntry> _channel =
        Channel.CreateBounded<SystemLogEntry>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryWrite(SystemLogEntry entry) => _channel.Writer.TryWrite(entry);

    public bool TryRead([NotNullWhen(true)] out SystemLogEntry? entry) =>
        _channel.Reader.TryRead(out entry);

    public IAsyncEnumerable<SystemLogEntry> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
