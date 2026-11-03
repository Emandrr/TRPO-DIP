using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaEkg.Application.Ecg;
using SaEkg.Domain.Constants;
using SaEkg.Infrastructure.Persistence;

namespace SaEkg.Infrastructure.Jobs;

public sealed class AnalyzeRecordWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AnalyzeRecordWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = new Task[AnalysisConstants.AnalysisParallelism];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = LoopAsync(stoppingToken);
        }

        await Task.WhenAll(workers);
    }

    private async Task LoopAsync(CancellationToken stoppingToken)
    {
        var workerId = Guid.NewGuid().ToString("N");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Guid? jobId = null;
                Guid? recordId = null;
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<SaEkgDbContext>();
                    var now = DateTimeOffset.UtcNow;
                    var stale = await db.BackgroundJobs
                        .Where(j => j.Type == "AnalyzeRecord" && j.Status == "Processing" && j.LockedUntil < now)
                        .ToListAsync(stoppingToken);
                    foreach (var s in stale)
                    {
                        s.Status = "Queued";
                        s.LockedBy = null;
                        s.LockedUntil = null;
                    }

                    if (stale.Count > 0)
                    {
                        await db.SaveChangesAsync(stoppingToken);
                    }

                    var job = await db.BackgroundJobs
                        .Where(j => j.Type == "AnalyzeRecord" && j.Status == "Queued" && j.RunAfter <= now)
                        .OrderBy(j => j.RunAfter)
                        .FirstOrDefaultAsync(stoppingToken);

                    if (job is null)
                    {
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    job.Status = "Processing";
                    job.LockedBy = workerId;
                    job.LockedUntil = now.AddMinutes(30);
                    job.Attempts += 1;
                    await db.SaveChangesAsync(stoppingToken);
                    jobId = job.Id;
                    recordId = JsonSerializer.Deserialize<JobPayload>(
                        job.PayloadJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })?.RecordId;
                }

                if (jobId is null || recordId is null || recordId == Guid.Empty)
                {
                    continue;
                }

                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var analysis = scope.ServiceProvider.GetRequiredService<IEcgAnalysisService>();
                    await analysis.AnalyzeRecordAsync(recordId.Value, stoppingToken);
                    await CompleteAsync(jobId.Value, success: true, null, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "AnalyzeRecord job failed for {RecordId}", recordId);
                    await CompleteAsync(jobId.Value, success: false, ex.Message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AnalyzeRecord worker loop error");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    private async Task CompleteAsync(Guid jobId, bool success, string? error, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SaEkgDbContext>();
        var job = await db.BackgroundJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        if (success)
        {
            job.Status = "Done";
            job.LastError = null;
        }
        else if (job.Attempts >= AnalysisConstants.MaxAnalysisAttempts)
        {
            job.Status = "Failed";
            job.LastError = error;
        }
        else
        {
            job.Status = "Queued";
            job.RunAfter = DateTimeOffset.UtcNow.AddSeconds(Math.Pow(2, job.Attempts));
            job.LastError = error;
        }

        job.LockedBy = null;
        job.LockedUntil = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed class JobPayload
    {
        public Guid RecordId { get; set; }
    }
}
