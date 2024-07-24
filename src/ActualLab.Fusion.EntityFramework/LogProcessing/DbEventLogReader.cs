using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public abstract class DbEventLogReader<TDbContext, TDbEntry, TOptions>(
    TOptions settings,
    IServiceProvider services
    ) : DbLogReader<TDbContext, Symbol, TDbEntry, TOptions>(settings, services), IDbLogReader
    where TDbContext : DbContext
    where TDbEntry : class, IDbEventLogEntry
    where TOptions : DbEventLogReaderOptions
{
    public override DbLogKind LogKind => DbLogKind.Events;

    protected override async Task<TDbEntry?> GetEntry(
        TDbContext dbContext, Symbol key, CancellationToken cancellationToken)
        => await dbContext.Set<TDbEntry>(LogKind.GetReadOneQueryHints())
            .FirstOrDefaultAsync(x => x.Uuid == key.Value, cancellationToken)
            .ConfigureAwait(false);

    protected override async Task<int> ProcessBatch(DbShard shard, int batchSize, CancellationToken cancellationToken)
    {
        using var _ = ActivitySource.IfEnabled(Settings.UseActivitySource).StartActivity(GetType()).AddShardTags(shard);
        var dbContext = await DbHub.CreateDbContext(shard, readWrite: true, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var _2 = tx.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        var now = SystemClock.Now.ToDateTime();
        var dbEntries = dbContext.Set<TDbEntry>();
        var entries = await dbEntries.WithHints(LogKind.GetReadBatchQueryHints())
            .Where(o => o.State == LogEntryState.New && o.DelayUntil < now)
            .OrderBy(o => o.DelayUntil)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (entries.Count == 0)
            return 0;

        var logLevel = entries.Count == batchSize ? LogLevel.Warning : LogLevel.Debug;
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        Log.IfEnabled(logLevel)?.Log(logLevel,
            $"{nameof(ProcessBatch)}[{{Shard}}]: got {{Count}}/{{BatchSize}} entries",
            shard.Value, entries.Count, batchSize);

        var results = await GetProcessTasks(shard, entries, cancellationToken)
            .Collect(Settings.ConcurrencyLevel)
            .ConfigureAwait(false);

        foreach (var (entry, isProcessed) in entries.Zip(results, static (entry, isProcessed) => (entry, isProcessed)))
            if (isProcessed)
                SetEntryState(dbEntries, entry, LogEntryState.Processed);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return entries.Count;
    }

    protected virtual IEnumerable<Task<bool>> GetProcessTasks(
        DbShard shard, List<TDbEntry> entries, CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
            yield return ProcessSafe(shard, entry.Uuid, entry, canReprocess: true, cancellationToken);
    }

    protected override async Task<bool> ProcessOne(
        DbShard shard, Symbol key, bool mustDiscard, CancellationToken cancellationToken)
    {
        var dbContext = await DbHub.CreateDbContext(shard, readWrite: true, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var _2 = tx.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        var entry = await GetEntry(dbContext, key, cancellationToken).ConfigureAwait(false);
        if (entry == null)
            throw new LogEntryNotFoundException();
        if (entry.State != LogEntryState.New)
            return false;

        var dbEntries = dbContext.Set<TDbEntry>();
        SetEntryState(dbEntries, entry, mustDiscard ? LogEntryState.Discarded : LogEntryState.Processed);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // If we're here, the entry's row is x-locked due to update above
        if (!mustDiscard)
            await Process(shard, entry, cancellationToken).ConfigureAwait(false);

        if (DbHub.ChaosMaker.IsEnabled)
            await DbHub.ChaosMaker.Act(this, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
