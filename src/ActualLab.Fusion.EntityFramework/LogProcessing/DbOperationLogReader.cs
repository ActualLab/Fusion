using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public abstract class DbOperationLogReader<TDbContext, TDbEntry, TOptions>(
    TOptions settings,
    IServiceProvider services
    ) : DbLogReader<TDbContext, long, TDbEntry, TOptions>(settings, services), IDbLogReader
    where TDbContext : DbContext
    where TDbEntry : class, IDbIndexedLogEntry
    where TOptions : DbOperationLogReaderOptions
{
    protected ConcurrentDictionary<DbShard, long> NextIndexes { get; } = new();

    public override DbLogKind LogKind => DbLogKind.Operations;

    protected override async Task<TDbEntry?> GetEntry(TDbContext dbContext, long key, CancellationToken cancellationToken)
        => await dbContext.Set<TDbEntry>(LogKind.GetReadOneQueryHints())
            .FirstOrDefaultAsync(x => x.Index == key, cancellationToken)
            .ConfigureAwait(false);

    protected override async Task<int> ProcessBatch(DbShard shard, int batchSize, CancellationToken cancellationToken)
    {
        var nextIndexOpt = await TryGetNextIndex(shard, cancellationToken).ConfigureAwait(false);
        if (nextIndexOpt is not { } nextIndex)
            return 0; // The log is empty

        using var _ = ActivitySource.StartActivity().AddShardTags(shard);
        var dbContext = await DbHub.CreateDbContext(shard, readWrite: true, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var _2 = tx.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        var dbEntries = dbContext.Set<TDbEntry>();
        var entries = await dbEntries.WithHints(LogKind.GetReadBatchQueryHints())
            // ReSharper disable once AccessToModifiedClosure
            .Where(o => o.Index >= nextIndex)
            .OrderBy(o => o.Index)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (entries.Count == 0)
            return 0;

        var logLevel = entries.Count == batchSize ? LogLevel.Warning : LogLevel.Debug;
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        Log.IfEnabled(logLevel)?.Log(logLevel,
            $"{nameof(ProcessBatch)}[{{Shard}}]: got {{Count}}/{{BatchSize}} log entries with Index >= {{LastIndex}}",
            shard.Value, entries.Count, batchSize, nextIndex);

        await GetProcessTasks(shard, entries, nextIndex, cancellationToken)
            .Collect(Settings.ConcurrencyLevel)
            .ConfigureAwait(false);
        NextIndexes[shard] = entries[^1].Index + 1;
        return entries.Count;
    }

    protected virtual IEnumerable<Task> GetProcessTasks(
        DbShard shard, List<TDbEntry> entries, long nextIndex, CancellationToken cancellationToken)
    {
        foreach (var entry in entries) {
            while (nextIndex != entry.Index)
                ReprocessSafe(shard, nextIndex++, false, cancellationToken);
            yield return ProcessSafe(shard, entry.Index, entry, canReprocess: true, cancellationToken);
            nextIndex++;
        }
    }

    protected override async Task<bool> ProcessOne(
        DbShard shard, long key, bool mustDiscard, CancellationToken cancellationToken)
    {
        if (mustDiscard)
            return true;

        var dbContext = await DbHub.CreateDbContext(shard, readWrite: true, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        var entry = await GetEntry(dbContext, key, cancellationToken).ConfigureAwait(false);
        if (entry == null)
            throw new LogEntryNotFoundException();
        if (entry.State != LogEntryState.New)
            return false;

        await Process(shard, entry, cancellationToken).ConfigureAwait(false);
        return true;
    }

    // Helpers

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

        var minLoggedAt = SystemClock.Now.ToDateTime() - Settings.StartOffset;
        return await dbContext.Set<TDbEntry>().AsQueryable()
            .Where(e => e.LoggedAt >= minLoggedAt)
            .OrderBy(e => e.LoggedAt).ThenBy(e => e.Index)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
