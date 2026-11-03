using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaEkg.Application.Ecg;
using SaEkg.Infrastructure.Persistence;
using Serilog.Context;

namespace SaEkg.Infrastructure.Services;

public sealed class PurgeService(
    SaEkgDbContext db,
    IEcgSignalStore signalStore,
    ILogger<PurgeService> logger) : IPurgeService
{
    public async Task<int> PurgeRawAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var due = await db.EcgRecords
            .Where(x => !x.RawPurged && x.RawPurgeAt < utcNow)
            .OrderBy(x => x.RawPurgeAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var record in due)
        {
            await signalStore.DeleteRawSamplesAsync(record.Id, cancellationToken);
            record.RawPurged = true;
            count++;
        }

        if (count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            using (LogContext.PushProperty("Module", "PurgeRaw"))
            using (LogContext.PushProperty("Code", "PURGE_RAW"))
            {
                logger.LogInformation("Purged raw samples for {Count} records", count);
            }
        }

        return count;
    }

    public async Task<int> PurgeFilteredAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var due = await db.EcgRecords
            .Where(x => !x.FilteredPurged && x.FilteredPurgeAt != null && x.FilteredPurgeAt < utcNow)
            .OrderBy(x => x.FilteredPurgeAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var record in due)
        {
            await signalStore.DeleteFilteredAndFiducialsAsync(record.Id, cancellationToken);
            record.FilteredPurged = true;
            count++;
        }

        if (count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            using (LogContext.PushProperty("Module", "PurgeFiltered"))
            using (LogContext.PushProperty("Code", "PURGE_FILTERED"))
            {
                logger.LogInformation("Purged filtered samples for {Count} records", count);
            }
        }

        return count;
    }
}
