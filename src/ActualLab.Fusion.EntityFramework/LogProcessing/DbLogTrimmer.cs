using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public record DbLogTrimmerOptions
{
    public TimeSpan MaxEntryAge { get; init; } = TimeSpan.FromMinutes(10);
    public int BatchSize { get; init; } = 1024;
    public RandomTimeSpan CheckPeriod { get; init; } = TimeSpan.FromMinutes(5).ToRandom(0.25);
    public RandomTimeSpan InterBatchDelay { get; init; } = TimeSpan.FromSeconds(0.05).ToRandom(0.25);
    public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(10));
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
}

public abstract class DbLogTrimmer<TDbContext, TDbEntry>(DbLogTrimmerOptions settings, IServiceProvider services)
    : DbShardWorkerBase<TDbContext>(services)
    where TDbContext : DbContext
    where TDbEntry : class, ILogEntry
{
    protected DbLogTrimmerOptions Settings { get; } = settings;
    protected IMomentClock SystemClock => Clocks.SystemClock;
    protected ILogger? DefaultLog => Log.IfEnabled(Settings.LogLevel);

    protected override Task OnRun(DbShard shard, CancellationToken cancellationToken)
        => new AsyncChain($"{nameof(TrimOldEntries)}[{shard}]", ct => TrimOldEntries(shard, ct))
            .RetryForever(Settings.RetryDelays, SystemClock, Log)
            .CycleForever()
            .Log(Log)
            .PrependDelay(Settings.InterBatchDelay, SystemClock)
            .Start(cancellationToken);

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
                if (count != batchSize)
                    break;

                await SystemClock.Delay(Settings.InterBatchDelay.Next(), cancellationToken).ConfigureAwait(false);
            }
            await SystemClock.Delay(Settings.CheckPeriod.Next(), cancellationToken).ConfigureAwait(false);
        }
    }

    protected virtual async Task<long> TrimBatch(DbShard shard, int batchSize, CancellationToken cancellationToken)
    {
        var minCommitTime = SystemClock.Now.ToDateTime() - Settings.MaxEntryAge;

        using var _ = ActivitySource.StartActivity().AddShardTags(shard);
        var dbContext = await CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        var lastCandidate = await dbContext.Set<TDbEntry>(DbHintSet.UpdateSkipLocked)
            .FirstOrDefaultAsync(e => e.CommitTime < minCommitTime, cancellationToken)
            .ConfigureAwait(false);
        if (lastCandidate == null)
            return 0;

#if NET7_0_OR_GREATER
        return await dbContext.Set<TDbEntry>(DbHintSet.UpdateSkipLocked)
            .Where(o => o.Index <= lastCandidate.Index)
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
}
