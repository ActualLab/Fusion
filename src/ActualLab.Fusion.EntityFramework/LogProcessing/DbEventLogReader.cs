using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.EntityFramework.Internal;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

/// <summary>
/// Abstract base for reading and processing event log entries from the database,
/// using exclusive row locking to ensure each event is processed exactly once.
/// </summary>
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

    protected override async Task<Moment> ProcessBatch(string shard, int batchSize, CancellationToken cancellationToken)
    {
        var startedAt = CpuTimestamp.Now;
        var entryCount = 0;
        var outcome = "success";
        var activity = FusionEntityFrameworkInstruments.ActivitySource
            .IfEnabled(Settings.IsTracingEnabled)
            .StartActivity(GetType())
            .AddShardTags(shard);
        try {
            var dbContext = await DbHub
                .CreateDbContext(shard, readWrite: true, cancellationToken)
                .ConfigureAwait(false);
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
            entryCount = entries.Count;
            if (entries.Count == 0)
                return await GetMinDelayUntil(dbEntries, cancellationToken).ConfigureAwait(false);

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
            foreach (var (entry, isProcessed) in entriesZipped) {
                if (isProcessed)
                    ReportProcessingDelay(shard, entry, "batch");
            }
            if (entries.Count >= batchSize)
                return default; // Full batch = there might be more entries

            // Partial batch - check if there are upcoming delayed entries
            return await GetMinDelayUntil(dbEntries, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) {
            outcome = e is OperationCanceledException ? "cancel" : "error";
            activity?.Finalize(e, cancellationToken);
            throw;
        }
        finally {
            activity?.Dispose();
            FusionEntityFrameworkInstruments.LogBatchSize.Record(entryCount,
                new KeyValuePair<string, object?>("log.kind", "event"),
                new KeyValuePair<string, object?>("outcome", outcome));
            FusionEntityFrameworkInstruments.LogBatchDuration.Record(startedAt.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("log.kind", "event"),
                new KeyValuePair<string, object?>("outcome", outcome));
        }
    }

    protected async Task<Moment> GetMinDelayUntil(DbSet<TDbEntry> dbEntries, CancellationToken cancellationToken)
    {
#if NET6_0_OR_GREATER
        var minDelayUntil = await dbEntries
#else
        var minDelayUntil = await dbEntries.AsQueryable()
#endif
            .Where(o => o.State == LogEntryState.New)
            .MinAsync(o => (DateTime?)o.DelayUntil, cancellationToken)
            .ConfigureAwait(false);
        return minDelayUntil.DefaultKind(DateTimeKind.Utc).ToMoment() ?? Moment.MaxValue;
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
        if (!mustDiscard)
            ReportProcessingDelay(shard, entry, "reprocess");
        return true;
    }

    protected virtual void ReportProcessingDelay(string shard, TDbEntry entry, string path)
    {
        var delay = SystemClock.Now.ToDateTime() - entry.DelayUntil;
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;
        FusionEntityFrameworkInstruments.EventLogProcessingDelay.Record(
            delay.TotalMilliseconds,
            new KeyValuePair<string, object?>("shard", shard),
            new KeyValuePair<string, object?>("path", path));
    }
}
