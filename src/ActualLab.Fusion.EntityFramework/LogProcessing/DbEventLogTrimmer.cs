using ActualLab.Fusion.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public abstract class DbEventLogTrimmer<TDbContext, TDbEntry, TOptions>(
    TOptions settings, IServiceProvider services)
    : DbShardWorkerBase<TDbContext>(services), IDbLogTrimmer
    where TDbContext : DbContext
    where TDbEntry : class, IDbEventLogEntry
    where TOptions : DbLogTrimmerOptions
{
    protected TOptions Settings { get; } = settings;
    protected MomentClock SystemClock => Clocks.SystemClock;
    protected ILogger? DefaultLog => Log.IfEnabled(Settings.LogLevel);

    public abstract DbLogKind LogKind { get; }

    protected override Task OnRun(string shard, CancellationToken cancellationToken)
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
    protected virtual async Task TrimOldEntries(string shard, CancellationToken cancellationToken)
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
        // ReSharper disable once FunctionNeverReturns
    }

    private async Task LogStatistics(string shard, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested) {
            await Task.Delay(Settings.StatisticsPeriod.Next(), cancellationToken).ConfigureAwait(false);

            var activity = FusionInstruments.ActivitySource
                .IfEnabled(Settings.IsTracingEnabled)
                .StartActivity(GetType())
                .AddShardTags(shard);
            try {
                var dbContext = await DbHub.CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
                await using var _1 = dbContext.ConfigureAwait(false);
                dbContext.EnableChangeTracking(false);

                var now = Clocks.SystemClock.Now.ToDateTime();
                var dbEntries = dbContext.Set<TDbEntry>().AsQueryable();
                var queuedCount = await dbEntries
                    .CountAsync(o => o.State == LogEntryState.New && o.DelayUntil > now, cancellationToken)
                    .ConfigureAwait(false);
                var pendingCount = await dbEntries
                    .CountAsync(o => o.State == LogEntryState.New && o.DelayUntil <= now, cancellationToken)
                    .ConfigureAwait(false);
                var processedCount = await dbEntries
                    .CountAsync(o => o.State == LogEntryState.Processed, cancellationToken)
                    .ConfigureAwait(false);
                var discardedCount = await dbEntries
                    .CountAsync(o => o.State == LogEntryState.Discarded, cancellationToken)
                    .ConfigureAwait(false);

                var totalCount = queuedCount + pendingCount + processedCount + discardedCount;
                Log.LogInformation(
                    "Statistics: {QueuedCount} queued, {PendingCount} pending, {ProcessedCount} processed, " +
                    "{DiscardedCount} discarded out of {TotalCount} entries",
                    queuedCount, pendingCount, processedCount, discardedCount, totalCount);
            }
            catch (Exception e) {
                activity?.Finalize(e, cancellationToken);
                throw;
            }
            finally {
                activity?.Dispose();
            }
        }
    }

    protected virtual async Task<long> TrimBatch(string shard, int batchSize, CancellationToken cancellationToken)
    {
        var minDelayUntil = SystemClock.Now.ToDateTime() - Settings.MaxEntryAge;

        var activity = FusionInstruments.ActivitySource
            .IfEnabled(Settings.IsTracingEnabled)
            .StartActivity(GetType())
            .AddShardTags(shard);
        try {
            var dbContext = await DbHub.CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
            await using var _1 = dbContext.ConfigureAwait(false);
            dbContext.EnableChangeTracking(false);

#if NET7_0_OR_GREATER
            return await dbContext.Set<TDbEntry>()
                .Where(o => o.DelayUntil <= minDelayUntil && o.State != LogEntryState.New)
                .OrderBy(o => o.DelayUntil).ThenBy(o => o.State)
                .Take(batchSize)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
#else
            var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var _2 = tx.ConfigureAwait(false);

            var entries = await dbContext.Set<TDbEntry>(DbHintSet.UpdateSkipLocked)
                .Where(o => o.DelayUntil <= minDelayUntil && o.State != LogEntryState.New)
                .OrderBy(o => o.DelayUntil).ThenBy(o => o.State)
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
            activity?.Finalize(e, cancellationToken);
            throw;
        }
        finally {
            activity?.Dispose();
        }
    }
}
