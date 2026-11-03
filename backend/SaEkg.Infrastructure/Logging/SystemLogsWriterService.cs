using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaEkg.Domain.Entities;
using SaEkg.Infrastructure.Persistence;

namespace SaEkg.Infrastructure.Logging;

public sealed class SystemLogsWriterService : BackgroundService
{
    private readonly SystemLogChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SystemLogsWriterService> _logger;

    public SystemLogsWriterService(
        SystemLogChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<SystemLogsWriterService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<SystemLogEntry>(64);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                batch.Clear();
                await foreach (var entry in _channel.ReadAllAsync(stoppingToken))
                {
                    batch.Add(entry);
                    while (batch.Count < 64 && _channel.TryRead(out var more))
                    {
                        batch.Add(more);
                    }

                    await FlushAsync(batch, stoppingToken);
                    batch.Clear();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist system logs batch");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task FlushAsync(List<SystemLogEntry> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SaEkgDbContext>();
            db.SystemLogs.AddRange(batch.Select(x => new SystemLog
            {
                Ts = x.Ts,
                Level = x.Level,
                Module = x.Module,
                Code = x.Code,
                Message = x.Message,
                UserId = x.UserId,
                RequestId = x.RequestId,
                DetailsJson = x.DetailsJson
            }));
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "system_logs sink unavailable; dropping {Count} entries", batch.Count);
        }
    }
}
