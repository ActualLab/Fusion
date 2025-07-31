using ActualLab.Fusion.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public abstract class DbEventLogReader<TDbContext, TDbEntry, TOptions>(
    TOptions settings,
    IServiceProvider services
    ) : DbLogReader<TDbContext, string, TDbEntry, TOptions>(settings, services), IDbLogReader
    where TDbContext : DbContext
    where TDbEntry : class, IDbEventLogEntry
    where TOptions : DbEventLogReaderOptions
{
    public override DbLogKind LogKind => DbLogKind.Events;

    protected override async Task<TDbEntry?> GetEntry(
        TDbContext dbContext, string key, CancellationToken cancellationToken)
        => await dbContext.Set<TDbEntry>(LogKind.GetReadOneQueryHints())
            .FirstOrDefaultAsync(x => x.Uuid == key, cancellationToken)
            .ConfigureAwait(false);

    protected override async Task<int> ProcessBatch(string shard, int batchSize, CancellationToken cancellationToken)
    {
        var activity = FusionInstruments.ActivitySource
            .IfEnabled(Settings.IsTracingEnabled)
            .StartActivity(GetType())
            .AddShardTags(shard);
        try {
            var dbContext = await DbHub.CreateDbContext(shard, readWrite: true, cancellationToken)
                .ConfigureAwait(false);
            await using var _1 = dbContext.ConfigureAwait(false);
            var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var _2 = tx.ConfigureAwait(false);
            dbContext.EnableChangeTracking(false);

            var now = SystemClock.Now.ToDateTime();
            var dbEntries = dbContext.Set<TDbEntry>();
            var entries = await dbEntries.WithHints(LogKind.GetReadBatchQueryHints())
                // It's .Where(o => o.State == LogEntryState.New && o.DelayUntil < now),
                // just written in a way that's more likely to utilize (State, DelayUntil) index
                .Where(o => o.State < LogEntryState.Processed && o.DelayUntil < now)
                .OrderBy(o => o.State).ThenBy(o => o.DelayUntil)
                .Take(batchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (entries.Count == 0)
                return 0;

            var logLevel = entries.Count == batchSize ? LogLevel.Warning : LogLevel.Debug;
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            Log.IfEnabled(logLevel)?.Log(logLevel,
                $"{nameof(ProcessBatch)}[{{Shard}}]: got {{Count}}/{{BatchSize}} entries",
                shard, entries.Count, batchSize);

            var results = await GetProcessTasks(shard, entries, cancellationToken)
                .Collect(Settings.ConcurrencyLevel, useCurrentScheduler: false, cancellationToken)
                .ConfigureAwait(false);

            var entriesZipped = entries.Zip(results, static (entry, isProcessed) => (entry, isProcessed));
            foreach (var (entry, isProcessed) in entriesZipped) {
                if (isProcessed)
                    SetEntryState(dbEntries, entry, LogEntryState.Processed);
            }
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return entries.Count;
        }
        catch (Exception e) {
            activity?.Finalize(e, cancellationToken);
            throw;
        }
        finally {
            activity?.Dispose();
        }
    }

    protected virtual IEnumerable<Task<bool>> GetProcessTasks(
        string shard, List<TDbEntry> entries, CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
            yield return ProcessSafe(shard, entry.Uuid, entry, canReprocess: true, cancellationToken);
    }

    protected override async Task<bool> ProcessOne(
        string shard, string key, bool mustDiscard, CancellationToken cancellationToken)
    {
        var dbContext = await DbHub.CreateDbContext(shard, readWrite: true, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var _2 = tx.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        var entry = await GetEntry(dbContext, key, cancellationToken).ConfigureAwait(false);
        if (entry is null)
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
