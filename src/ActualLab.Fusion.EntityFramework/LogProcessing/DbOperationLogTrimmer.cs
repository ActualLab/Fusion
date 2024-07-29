using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public abstract class DbOperationLogTrimmer<TDbContext, TDbEntry, TOptions>(
    TOptions settings, IServiceProvider services)
    : DbShardWorkerBase<TDbContext>(services), IDbLogTrimmer
    where TDbContext : DbContext
    where TDbEntry : class, IDbIndexedLogEntry
    where TOptions : DbLogTrimmerOptions
{
    protected TOptions Settings { get; } = settings;
    protected MomentClock SystemClock => Clocks.SystemClock;
    protected ILogger? DefaultLog => Log.IfEnabled(Settings.LogLevel);

    public abstract DbLogKind LogKind { get; }

    protected override Task OnRun(DbShard shard, CancellationToken cancellationToken)
    {
        var mainTask = new AsyncChain($"{nameof(TrimOldEntries)}[{shard}]", ct => TrimOldEntries(shard, ct))
            .RetryForever(Settings.RetryDelays, SystemClock, Log)
            .CycleForever()
            .Log(Log)
            .PrependDelay(Settings.CheckPeriod.Next().MultiplyBy(0.1), SystemClock)
            .Start(cancellationToken);
        var statisticsTask = new AsyncChain($"{nameof(LogStatistics)}[{shard}]", ct => LogStatistics(shard, ct))
            .Silence()
            .CycleForever()
            .Log(Log)
            .Start(cancellationToken);
        return Task.WhenAll(mainTask, statisticsTask);
    }

    protected virtual async Task TrimOldEntries(DbShard shard, CancellationToken cancellationToken)
    {
        var batchSize = Settings.BatchSize;
        while (true) {
            while (true) { // Reading entries in batches
                var count = await TrimBatch(shard, batchSize, cancellationToken).ConfigureAwait(false);
                if (count > 0)
                    DefaultLog?.Log(Settings.LogLevel,
                        $"{nameof(TrimOldEntries)}[{{Shard}}]: trimmed {{Count}} entries",
                        shard, count);
                if (count < batchSize)
                    break;
            }
            await SystemClock.Delay(Settings.CheckPeriod.Next(), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task LogStatistics(DbShard shard, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested) {
            await Task.Delay(Settings.StatisticsPeriod.Next(), cancellationToken).ConfigureAwait(false);

            var activity = ActivitySource.IfEnabled(Settings.UseActivitySource).StartActivity(GetType()).AddShardTags(shard);
            try {
                var dbContext = await DbHub.CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
                await using var _1 = dbContext.ConfigureAwait(false);
                dbContext.EnableChangeTracking(false);

                var dbEntries = dbContext.Set<TDbEntry>().AsQueryable();
                var firstEntry = await dbEntries
                    .OrderBy(o => o.LoggedAt)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                var totalCount = await dbEntries
                    .CountAsync(cancellationToken)
                    .ConfigureAwait(false);

                var firstEntryAge = Clocks.SystemClock.Now - firstEntry?.LoggedAt.ToMoment();
                var entryRate = firstEntryAge is { } interval ? totalCount / interval.TotalSeconds : 0;
                Log.LogInformation("Statistics: {TotalCount} entries, +{EntryRate} entries/s", totalCount, entryRate);
            }
            catch (Exception e) {
                activity?.MaybeSetError(e, cancellationToken);
                throw;
            }
            finally {
                activity?.Dispose();
            }
        }
    }

    protected virtual async Task<long> TrimBatch(DbShard shard, int batchSize, CancellationToken cancellationToken)
    {
        var minLoggedAt = SystemClock.Now.ToDateTime() - Settings.MaxEntryAge;

        var activity = ActivitySource.IfEnabled(Settings.UseActivitySource).StartActivity(GetType()).AddShardTags(shard);
        try {
            var dbContext = await DbHub.CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
            await using var _1 = dbContext.ConfigureAwait(false);
            dbContext.EnableChangeTracking(false);

            var lastCandidate = await dbContext.Set<TDbEntry>(DbHintSet.UpdateSkipLocked)
                .FirstOrDefaultAsync(e => e.LoggedAt < minLoggedAt, cancellationToken)
                .ConfigureAwait(false);
            if (lastCandidate == null)
                return 0;

#if NET7_0_OR_GREATER
            return await dbContext.Set<TDbEntry>()
                .Where(o => o.Index <= lastCandidate.Index)
                .OrderBy(o => o.Index)
                .Take(batchSize)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
#else
            var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var _2 = tx.ConfigureAwait(false);

            var entries = await dbContext.Set<TDbEntry>(DbHintSet.UpdateSkipLocked)
                .Where(o => o.Index <= lastCandidate.Index)
                .OrderBy(o => o.Index)
                .Take(batchSize)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            if (entries.Count == 0)
                return 0;

            dbContext.Set<TDbEntry>().RemoveRange(entries);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return entries.Count;
#endif
        }
        catch (Exception e) {
            activity?.MaybeSetError(e, cancellationToken);
            throw;
        }
        finally {
            activity?.Dispose();
        }
    }
}
