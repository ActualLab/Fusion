using ActualLab.Internal;
using ActualLab.Resilience;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public abstract class DbOperationLogReader<TDbContext, TDbEntry, TOptions>(
    TOptions settings,
    IServiceProvider services
    ) : DbLogReader<TDbContext, TDbEntry, TOptions>(settings, services), IDbLogReader
    where TDbContext : DbContext
    where TDbEntry : class, IDbIndexedLogEntry
    where TOptions : DbOperationLogReaderOptions
{
    protected ConcurrentDictionary<DbShard, long> NextIndexes { get; } = new();
    protected Dictionary<(DbShard Shard, long Index), Task> ReprocessTasks { get; } = new();

    public override DbLogKind LogKind => DbLogKind.Operations;

    protected override async Task<bool> ProcessBatch(DbShard shard, CancellationToken cancellationToken)
    {
        var nextIndexOpt = await TryGetNextIndex(shard, cancellationToken).ConfigureAwait(false);
        if (nextIndexOpt is not { } nextIndex)
            return false; // The log is empty

        using var _ = ActivitySource.StartActivity().AddShardTags(shard);
        var dbContext = await DbHub.CreateDbContext(shard, readWrite: true, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var _2 = tx.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        var batchSize = Settings.BatchSize;
        var dbEntries = dbContext.Set<TDbEntry>();
        var entries = await dbEntries.WithHints(LogKind.GetReadBatchQueryHints())
            // ReSharper disable once AccessToModifiedClosure
            .Where(o => o.Index >= nextIndex)
            .OrderBy(o => o.Index)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var logLevel = entries.Count == batchSize ? LogLevel.Warning : LogLevel.Debug;
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        Log.IfEnabled(logLevel)?.Log(logLevel,
            $"{nameof(ProcessBatch)}[{{Shard}}]: fetched {{Count}}/{{BatchSize}} log entries with Index >= {{LastIndex}}",
            shard.Value, entries.Count, batchSize, nextIndex);

        if (entries.Count == 0)
            return false;

        await GetProcessTasks(shard, entries, nextIndex, cancellationToken)
            .Collect(Settings.ConcurrencyLevel)
            .ConfigureAwait(false);
        nextIndex = entries[^1].Index + 1;

        NextIndexes[shard] = nextIndex;
        return entries.Count >= batchSize;
    }

    protected async ValueTask<long?> TryGetNextIndex(DbShard shard, CancellationToken cancellationToken)
    {
        if (NextIndexes.TryGetValue(shard, out var nextIndex))
            return nextIndex;

        var startEntry = await GetStartEntry(shard, cancellationToken).ConfigureAwait(false);
        if (startEntry == null)
            return null;

        nextIndex = NextIndexes.GetOrAdd(shard, startEntry.Index);
        DefaultLog?.Log(Settings.LogLevel,
            $"{nameof(ProcessNewEntries)}[{{Shard}}]: starting from #{{StartIndex}}",
            shard, nextIndex);
        return nextIndex;
    }

    protected virtual async Task<TDbEntry?> GetStartEntry(DbShard shard, CancellationToken cancellationToken)
    {
        var dbContext = await DbHub.CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _ = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        var candidateEntries = dbContext.Set<TDbEntry>().AsQueryable();
        if (Settings is DbOperationLogReaderOptions cooperativeSettings) {
            var minLoggedAt = SystemClock.Now.ToDateTime() - cooperativeSettings.StartOffset;
            candidateEntries = candidateEntries.Where(e => e.LoggedAt >= minLoggedAt);
        }
        else
            candidateEntries = candidateEntries.Where(e => e.State == LogEntryState.New);
        return await candidateEntries
            .OrderBy(e => e.Index)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    protected virtual IEnumerable<Task> GetProcessTasks(
        DbShard shard, List<TDbEntry> entries, long nextIndex, CancellationToken cancellationToken)
    {
        foreach (var entry in entries) {
            while (nextIndex != entry.Index)
                ReprocessSafe(shard, nextIndex++, entry.LoggedAt, null);
            yield return ProcessSafe(shard, entry, fallbackToReprocess: true, cancellationToken);
            nextIndex++;
        }
    }

    protected async Task ProcessSafe(
        DbShard shard, TDbEntry entry, bool fallbackToReprocess, CancellationToken cancellationToken)
    {
        // This method should never fail (unless cancelled)!
        if (entry.State != LogEntryState.New)
            return;

        try {
            await Process(shard, entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            var suffix = fallbackToReprocess ? ", will reprocess it" : "";
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            Log.LogError(e,
                $"{nameof(Process)}[{{Shard}}]: failed for entry #{{Index}}{suffix}",
                shard, entry.Index);
            if (fallbackToReprocess)
                ReprocessSafe(shard, entry.Index, entry.LoggedAt, entry);
        }
    }

    protected void ReprocessSafe(DbShard shard, long index, Moment maxLoggedAt, TDbEntry? foundEntry)
    {
        // This method should never fail!
        lock (ReprocessTasks) {
            var key = (shard, index);
            if (ReprocessTasks.ContainsKey(key))
                return;

            var task = Task.Run(() => Reprocess(shard, index, maxLoggedAt, foundEntry, StopToken));
            ReprocessTasks[key] = task;
            _ = task.ContinueWith(_ => {
                lock (ReprocessTasks)
                    ReprocessTasks.Remove(key);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    protected async Task Reprocess(
        DbShard shard, long index, Moment loggedAt, TDbEntry? foundEntry, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 2; i++) {
            var (mustDiscard, sProcess, sProcessed, sErrorExtra) = i switch {
                0 => (false, "process", "processed", ", will try to discard it"),
                _ => (true, "discard", "discarded", ""),
            };
            try {
                await Task.Delay(Settings.ReprocessDelay.Next(), cancellationToken).ConfigureAwait(false);
                var isProcessed = await Settings.ReprocessPolicy
                    .Apply(ct => ReprocessImpl(shard, index, mustDiscard, ct), cancellationToken)
                    .ConfigureAwait(false);
                if (isProcessed) {
                    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                    DefaultLog?.Log(Settings.LogLevel,
                        $"{nameof(Reprocess)}[{{Shard}}]: entry #{{Index}} is {sProcessed}",
                        shard, index);
                    return;
                }

                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                DebugLog?.LogDebug(
                    $"{nameof(Reprocess)}[{{Shard}}]: entry #{{Index}} is processed by another host",
                    shard, index);
                return;
            }
            catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                Log.LogError(e,
                    $"{nameof(Reprocess)}[{{Shard}}]: failed to {sProcess} entry #{{Index}}{sErrorExtra}",
                    shard, index);
            }
        }
    }

    protected async Task<bool> ReprocessImpl(
        DbShard shard, long index, bool mustDiscard, CancellationToken cancellationToken)
    {
        var dbContext = await DbHub.CreateDbContext(shard, readWrite: true, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        if (mustDiscard)
            throw Errors.InternalError("Can't discard the entry from co-processed log.");

        var entry = await GetEntry(dbContext, index, cancellationToken).ConfigureAwait(false);
        if (entry == null)
            throw new LogEntryNotFoundException();

        await ProcessSafe(shard, entry, false, cancellationToken).ConfigureAwait(false);
        return true;
    }

    protected async Task<TDbEntry?> GetEntry(TDbContext dbContext, long index, CancellationToken cancellationToken)
        => await dbContext.Set<TDbEntry>(LogKind.GetReadOneQueryHints())
            .FirstOrDefaultAsync(x => x.Index == index, cancellationToken)
            .ConfigureAwait(false);
}
