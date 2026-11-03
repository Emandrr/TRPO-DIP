using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaEkg.Application.Ecg;

namespace SaEkg.Infrastructure.Jobs;

public sealed class PurgeJobsWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PurgeJobsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var purge = scope.ServiceProvider.GetRequiredService<IPurgeService>();
                var now = DateTimeOffset.UtcNow;
                await purge.PurgeRawAsync(now, stoppingToken);
                await purge.PurgeFilteredAsync(now, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Purge jobs cycle failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
