using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.EntityFramework.Internal;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

/// <summary>
/// Abstract base for reading and processing indexed operation log entries from the database,
/// tracking the next expected index per shard.
/// </summary>
public abstract class DbOperationLogReader<TDbContext, TDbEntry, TOptions>(
    TOptions settings,
    IServiceProvider services
    ) : DbLogReader<TDbContext, long, TDbEntry, TOptions>(settings, services), IDbLogReader
    where TDbContext : DbContext
    where TDbEntry : class, IDbIndexedLogEntry
    where TOptions : DbOperationLogReaderOptions
{
    protected ConcurrentDictionary<string, long> NextIndexes { get; } = new(StringComparer.Ordinal);
    // Per-shard set of unresolved indexes: gaps (missing entries) and present-but-failing entries.
    // Re-checked in batches on an adaptive cadence - see ProcessGaps.
    protected ConcurrentDictionary<string, ShardGapSet> Gaps { get; } = new(StringComparer.Ordinal);
    protected ConcurrentDictionary<string, DelayWarningState> DelayWarningStates { get; }
        = new(StringComparer.Ordinal);

    public override DbLogKind LogKind => DbLogKind.Operations;

    protected override async Task<TDbEntry?> GetEntry(TDbContext dbContext, long key, CancellationToken cancellationToken)
        => await dbContext.Set<TDbEntry>(LogKind.GetReadOneQueryHints())
            .FirstOrDefaultAsync(x => x.Index == key, cancellationToken)
            .ConfigureAwait(false);

    protected override async Task<Moment> ProcessBatch(string shard, int batchSize, CancellationToken cancellationToken)
    {
        var nextIndexOpt = await TryGetNextIndex(shard, cancellationToken).ConfigureAwait(false);
        if (nextIndexOpt is not { } nextIndex)
            return await ProcessGaps(shard, cancellationToken).ConfigureAwait(false); // The log is empty

        var activity = FusionInstruments.ActivitySource
            .IfEnabled(Settings.IsTracingEnabled)
            .StartActivity(GetType())
            .AddShardTags(shard);
        try {
            List<TDbEntry> entries;
            var dbContext = await DbHub
                .CreateDbContext(shard, readWrite: true, cancellationToken).ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false)) {
                var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await using var _1 = tx.ConfigureAwait(false);
                dbContext.EnableChangeTracking(false);

                var dbEntries = dbContext.Set<TDbEntry>();
                entries = await dbEntries.WithHints(LogKind.GetReadBatchQueryHints())
                    // ReSharper disable once AccessToModifiedClosure
                    .Where(o => o.Index >= nextIndex)
                    .OrderBy(o => o.Index)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (entries.Count != 0 && entries[0].Index > nextIndex)
                    nextIndex = await ResolveFrontGap(shard, dbContext, nextIndex, entries[0].Index, cancellationToken)
                        .ConfigureAwait(false);
            }
            if (entries.Count == 0)
                return await ProcessGaps(shard, cancellationToken).ConfigureAwait(false); // No more entries

            var logLevel = entries.Count == batchSize ? LogLevel.Warning : LogLevel.Debug;
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            Log.IfEnabled(logLevel)?.Log(logLevel,
                $"{nameof(ProcessBatch)}[{{Shard}}]: got {{Count}}/{{BatchSize}} log entries with Index >= {{LastIndex}}",
                shard, entries.Count, batchSize, nextIndex);

            await GetProcessTasks(shard, entries, nextIndex, cancellationToken)
                .Collect(Settings.ConcurrencyLevel, useCurrentScheduler: false, cancellationToken)
                .ConfigureAwait(false);
            NextIndexes[shard] = entries[^1].Index + 1;
            // The gap check runs on full batches too - the due-filter makes it cheap,
            // so gap processing can't starve while the shard is busy
            var nextGapCheckAt = await ProcessGaps(shard, cancellationToken).ConfigureAwait(false);
            return entries.Count >= batchSize
                ? default // Full batch = there might be more entries, read again immediately
                : nextGapCheckAt;
        }
        catch (Exception e) {
            activity?.Finalize(e, cancellationToken);
            throw;
        }
        finally {
            activity?.Dispose();
        }
    }

    protected virtual IEnumerable<Task> GetProcessTasks(
        string shard, List<TDbEntry> entries, long nextIndex, CancellationToken cancellationToken)
    {
        var now = SystemClock.Now;
        foreach (var entry in entries) {
            while (nextIndex != entry.Index)
                AddGap(shard, nextIndex++, now);
            yield return ProcessBatchEntry(shard, entry, cancellationToken);
            nextIndex++;
        }
    }

    private async Task ProcessBatchEntry(string shard, TDbEntry entry, CancellationToken cancellationToken)
    {
        var isProcessed = await ProcessSafe(shard, entry.Index, entry, canReprocess: true, cancellationToken)
            .ConfigureAwait(false);
        if (isProcessed)
            ReportProcessingDelay(shard, entry, "batch");
    }

    protected override async Task<bool> ProcessOne(
        string shard, long key, bool mustDiscard, CancellationToken cancellationToken)
    {
        if (mustDiscard)
            return true;

        var dbContext = await DbHub.CreateDbContext(shard, readWrite: true, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        var entry = await GetEntry(dbContext, key, cancellationToken).ConfigureAwait(false);
        if (entry is null)
            throw new LogEntryNotFoundException();
        if (entry.State != LogEntryState.New)
            return false;

        await Process(shard, entry, cancellationToken).ConfigureAwait(false);
        ReportProcessingDelay(shard, entry, "reprocess");
        return true;
    }

    // Present-but-failing entries (in practice: ToModel() deserialization failures) that outlived the
    // quick ReprocessPolicy join the pending-gap set with a bounded retry budget - see item 3 (residual).
    protected override void OnReprocessExhausted(string shard, long key, CancellationToken cancellationToken)
        => AddGap(shard, key, SystemClock.Now, Settings.FailedEntryRetryLimit);

    // Protected/internal methods

    protected async ValueTask<long?> TryGetNextIndex(string shard, CancellationToken cancellationToken)
    {
        if (NextIndexes.TryGetValue(shard, out var nextIndex))
            return nextIndex;

        var startEntry = await GetStartEntry(shard, cancellationToken).ConfigureAwait(false);
        if (startEntry is null)
            return null;

        nextIndex = NextIndexes.GetOrAdd(shard, startEntry.Index);
        DefaultLog?.Log(Settings.LogLevel,
            $"{nameof(ProcessNewEntries)}[{{Shard}}]: starting from #{{StartIndex}}",
            shard, nextIndex);
        return nextIndex;
    }

    protected virtual async Task<TDbEntry?> GetStartEntry(string shard, CancellationToken cancellationToken)
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

    // A front gap (entries[0].Index > nextIndex) is either a normal gap (in-flight commit or identity
    // jump) - older entries still exist - or coverage loss (item 10): the reader stalled past the trim
    // age, so its cursor is below the oldest surviving entry. The latter is remediated pessimistically.
    protected virtual async Task<long> ResolveFrontGap(
        string shard, TDbContext dbContext, long nextIndex, long firstIndex, CancellationToken cancellationToken)
    {
        var hasOlder = await dbContext.Set<TDbEntry>().AsQueryable()
            .Where(e => e.Index < nextIndex)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
        if (hasOlder)
            return nextIndex;

        Log.LogError(
            "{Method}[{Shard}]: operation-log coverage lost - cursor #{NextIndex} is below the oldest " +
            "surviving entry #{FirstIndex}; invalidating all locally registered computed values",
            nameof(ResolveFrontGap), shard, nextIndex, firstIndex);
        ComputedRegistry.InvalidateEverything();
        if (Gaps.TryGetValue(shard, out var shardGaps))
            lock (shardGaps)
                shardGaps.Entries.Clear();
        NextIndexes[shard] = firstIndex;
        return firstIndex;
    }

    protected void AddGap(string shard, long index, Moment now, int retriesLeft = -1)
    {
        var shardGaps = Gaps.GetOrAdd(shard, static _ => new ShardGapSet());
        lock (shardGaps) {
            var entries = shardGaps.Entries;
            if (entries.ContainsKey(index))
                return;
            if (entries.Count >= Settings.GapSetSizeLimit) {
                if (!shardGaps.SizeLimitWarned) {
                    shardGaps.SizeLimitWarned = true;
                    Log.LogError(
                        "{Method}[{Shard}]: pending-gap set reached its size limit of {Limit}, dropping gap #{Index}",
                        nameof(AddGap), shard, Settings.GapSetSizeLimit, index);
                }
                return;
            }
            entries[index] = new GapEntry(now, retriesLeft);
        }
    }

    protected virtual async Task<Moment> ProcessGaps(string shard, CancellationToken cancellationToken)
    {
        if (!Gaps.TryGetValue(shard, out var shardGaps))
            return Moment.MaxValue;

        var now = SystemClock.Now;
        long[] indexesToCheck;
        lock (shardGaps) {
            var entries = shardGaps.Entries;
            // Every entry expires at the retention horizon - past it its row is trimmed anyway.
            // Pure gaps (aborted tx / identity jump) go silently; a budgeted entry is a real loss,
            // so it goes with an error - same principle as budget-exhaustion abandonment.
            var horizon = now - Settings.GapRetentionPeriod;
            List<long>? expiredGaps = null;
            List<long>? expiredFailed = null;
            foreach (var (index, gap) in entries) {
                if (gap.AddedAt >= horizon)
                    continue;

                if (gap.RetriesLeft < 0)
                    (expiredGaps ??= []).Add(index);
                else
                    (expiredFailed ??= []).Add(index);
            }
            if (expiredGaps != null) {
                foreach (var index in expiredGaps)
                    entries.Remove(index);
                DefaultLog?.Log(Settings.LogLevel,
                    $"{nameof(ProcessGaps)}[{{Shard}}]: dropped {{Count}} gap(s) past the retention horizon",
                    shard, expiredGaps.Count);
            }
            if (expiredFailed != null)
                foreach (var index in expiredFailed) {
                    entries.Remove(index);
                    Log.LogError(
                        "{Method}[{Shard}]: failed entry #{Index} outlived the retention horizon, abandoning it",
                        nameof(ProcessGaps), shard, index);
                }
            if (IsDrained(shardGaps))
                return Moment.MaxValue;

            // NextAttemptAt gates all re-queries: a wake-up with nothing due issues zero queries
            List<long>? due = null;
            foreach (var (index, gap) in entries)
                if (gap.NextAttemptAt <= now)
                    (due ??= []).Add(index);
            indexesToCheck = due?.ToArray() ?? [];
        }

        // Batched existence re-check via chunked Index-Contains queries
        foreach (var chunk in indexesToCheck.Chunk(Settings.GapCheckChunkSize)) {
            var found = await GetGapEntries(shard, chunk, cancellationToken).ConfigureAwait(false);
            foreach (var entry in found)
                await ProcessGapEntry(shard, shardGaps, entry, cancellationToken).ConfigureAwait(false);
        }

        lock (shardGaps) {
            var entries = shardGaps.Entries;
            if (IsDrained(shardGaps))
                return Moment.MaxValue;
            // Advance the cadence of checked-but-still-missing entries: young pure gaps poll fast,
            // everything else at ~CheckPeriod. Found entries were removed or advanced in ProcessGapEntry.
            var fastCheckAge = Settings.GapFastCheckAge;
            foreach (var index in indexesToCheck) {
                if (!entries.TryGetValue(index, out var gap) || gap.NextAttemptAt > now)
                    continue;

                var isYoungGap = gap.RetriesLeft < 0 && now - gap.AddedAt < fastCheckAge;
                gap.NextAttemptAt = now + (isYoungGap ? Settings.GapCheckPeriod : Settings.CheckPeriod).Next();
            }
            var nextCheckAt = Moment.MaxValue;
            foreach (var gap in entries.Values)
                nextCheckAt = Moment.Min(nextCheckAt, gap.NextAttemptAt);
            return nextCheckAt;
        }

        static bool IsDrained(ShardGapSet shardGaps)
        {
            if (shardGaps.Entries.Count != 0)
                return false;

            shardGaps.SizeLimitWarned = false;
            return true;
        }
    }

    protected virtual async Task<List<TDbEntry>> GetGapEntries(
        string shard, long[] indexes, CancellationToken cancellationToken)
    {
        var dbContext = await DbHub.CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _ = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        // Cooperative read hints are a no-op for the operation log (no locking-read DbHintFormatter);
        // MVCC snapshot reads never block on uncommitted inserts, so no skip-locked hint is needed there.
        return await dbContext.Set<TDbEntry>(LogKind.GetReadBatchQueryHints())
            .Where(e => indexes.Contains(e.Index))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    protected virtual async Task ProcessGapEntry(
        string shard, ShardGapSet shardGaps, TDbEntry entry, CancellationToken cancellationToken)
    {
        var index = entry.Index;
        if (entry.State != LogEntryState.New) {
            lock (shardGaps)
                shardGaps.Entries.Remove(index);
            return;
        }
        try {
            await Process(shard, entry, cancellationToken).ConfigureAwait(false);
            lock (shardGaps)
                shardGaps.Entries.Remove(index);
            ReportProcessingDelay(shard, entry, "gap");
            DebugLog?.LogDebug(
                $"{nameof(ProcessGaps)}[{{Shard}}]: entry #{{Index}} is processed",
                shard, index);
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            bool abandon;
            var retriesLeft = 0;
            lock (shardGaps) {
                if (!shardGaps.Entries.TryGetValue(index, out var gap))
                    return;
                if (gap.RetriesLeft < 0)
                    gap.RetriesLeft = Settings.FailedEntryRetryLimit;
                retriesLeft = --gap.RetriesLeft;
                abandon = retriesLeft <= 0;
                if (abandon)
                    shardGaps.Entries.Remove(index);
                else
                    gap.NextAttemptAt = SystemClock.Now + Settings.CheckPeriod.Next();
            }
            if (abandon)
                Log.LogError(e,
                    "{Method}[{Shard}]: entry #{Index} failed and its retry budget is exhausted, abandoning it",
                    nameof(ProcessGaps), shard, index);
            else
                DebugLog?.LogDebug(e,
                    $"{nameof(ProcessGaps)}[{{Shard}}]: entry #{{Index}} failed, {{RetriesLeft}} retries left",
                    shard, index, retriesLeft);
        }
    }

    protected virtual void ReportProcessingDelay(string shard, TDbEntry entry, string path)
    {
        var delay = SystemClock.Now.ToDateTime() - entry.LoggedAt;
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero; // Cross-host clock drift can make it slightly negative
        FusionEntityFrameworkInstruments.OperationLogProcessingDelay.Record(
            delay.TotalMilliseconds,
            new KeyValuePair<string, object?>("shard", shard),
            new KeyValuePair<string, object?>("path", path));

        var threshold = Settings.ProcessingDelayWarningThreshold;
        if (threshold <= TimeSpan.Zero || delay <= threshold)
            return;

        int suppressedCount;
        TimeSpan maxSuppressedDelay;
        var state = DelayWarningStates.GetOrAdd(shard, static _ => new DelayWarningState());
        lock (state) {
            var now = SystemClock.Now;
            if (now < state.NextWarningAt) {
                state.SuppressedCount++;
                state.MaxSuppressedDelay = TimeSpanExt.Max(state.MaxSuppressedDelay, delay);
                return;
            }

            (suppressedCount, maxSuppressedDelay) = (state.SuppressedCount, state.MaxSuppressedDelay);
            state.SuppressedCount = 0;
            state.MaxSuppressedDelay = default;
            state.NextWarningAt = now + Settings.ProcessingDelayWarningPeriod;
        }
        if (suppressedCount > 0)
            Log.LogWarning(
                $"{nameof(ReportProcessingDelay)}[{{Shard}}]: entry #{{Index}} was processed {{Delay}} " +
                "after logging ({Path} path); +{SuppressedCount} more delayed entries " +
                "(max {MaxDelay}) since the last warning",
                shard, entry.Index, delay.ToShortString(), path,
                suppressedCount, maxSuppressedDelay.ToShortString());
        else
            Log.LogWarning(
                $"{nameof(ReportProcessingDelay)}[{{Shard}}]: entry #{{Index}} was processed {{Delay}} " +
                "after logging ({Path} path)",
                shard, entry.Index, delay.ToShortString(), path);
    }

    // Nested types

    protected sealed class GapEntry(Moment addedAt, int retriesLeft)
    {
        public Moment AddedAt { get; } = addedAt;
        // -1 = pure gap (waits for the retention horizon); >= 0 = present-but-failing (bounded budget)
        public int RetriesLeft { get; set; } = retriesLeft;
        // The single cadence authority for all entries: due when <= now,
        // advanced after every miss or failed attempt
        public Moment NextAttemptAt { get; set; } = addedAt;
    }

    protected sealed class ShardGapSet
    {
        public Dictionary<long, GapEntry> Entries { get; } = new();
        public bool SizeLimitWarned { get; set; }
    }

    protected sealed class DelayWarningState
    {
        public Moment NextWarningAt;
        public int SuppressedCount;
        public TimeSpan MaxSuppressedDelay;
    }
}
