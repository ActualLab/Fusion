using ActualLab.OS;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public enum DbLogProcessingMode
{
    Cooperative = 0, // Every reader processes each entry - used for operation log / invalidations
    Exclusive = 1, // Just a single reader processes each entry - used for outbox items
}

public abstract record DbLogProcessorOptions
{
    public int BatchSize { get; init; } = 128;
    public TimeSpan MaxGapLifespan { get; init; }
    public RetryDelaySeq ProcessGapDelays { get; init; } = null!;
    public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(1, 5);
    public RandomTimeSpan ForcedCheckPeriod { get; init; } = TimeSpan.FromSeconds(5).ToRandom(0.1);
    public int ProcessConcurrencyLevel { get; init; }
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
}

public abstract record CooperativeDbLogProcessorOptions : DbLogProcessorOptions
{
    protected CooperativeDbLogProcessorOptions()
    {
        MaxGapLifespan = TimeSpan.FromSeconds(3);
        ProcessGapDelays = RetryDelaySeq.Exp(0.25, 1, 0.1, 2); // Up to 1 second, 2x longer on each iteration
        ProcessConcurrencyLevel = HardwareInfo.GetProcessorCountFactor(4);
    }
}

public abstract record ExclusiveDbLogProcessorOptions : DbLogProcessorOptions
{
    protected ExclusiveDbLogProcessorOptions()
    {
        MaxGapLifespan = TimeSpan.FromMinutes(5); // Must be longer than any possible transaction
        ProcessGapDelays = RetryDelaySeq.Exp(0.25, 10, 0.1, 2); // Up to 10 seconds, 2x longer on each iteration
        ProcessConcurrencyLevel = HardwareInfo.GetProcessorCountFactor(4);
    }
}

public static class DbLogProcessor
{
    public static DbHint[] CooperativeReadHints { get; set; } = DbHintSet.Empty;
    public static DbHint[] ExclusiveReadHints { get; set; } =  DbHintSet.UpdateSkipLocked;

    public static DbHint[] GetProcessQueryHints(DbLogProcessingMode mode)
        => mode is DbLogProcessingMode.Cooperative
            ? CooperativeReadHints
            : ExclusiveReadHints;
}

public abstract class DbLogProcessor<TDbContext, TDbEntry, TOptions>(
    TOptions settings,
    IServiceProvider services)
    : DbShardWorkerBase<TDbContext>(services)
    where TDbContext : DbContext
    where TDbEntry : class, ILogEntry
    where TOptions : DbLogProcessorOptions
{
    protected ConcurrentDictionary<DbShard, long> NextIndexes { get; } = new();
    protected Dictionary<(DbShard Shard, long Index), Task> ProcessGapTasks { get; } = new();

    protected IDbLogWatcher<TDbContext, TDbEntry> LogWatcher { get; } = services.DbLogWatcher<TDbContext, TDbEntry>();
    protected IDbEntityResolver<long, TDbEntry> EntryResolver { get; } = services.DbEntityResolver<long, TDbEntry>();
    protected IMomentClock SystemClock { get; init; } = services.Clocks().SystemClock;
    protected ILogger? DefaultLog => Log.IfEnabled(Settings.LogLevel);

    public TOptions Settings { get; } = settings;
    public DbLogProcessingMode Mode { get; } = settings switch {
        CooperativeDbLogProcessorOptions => DbLogProcessingMode.Cooperative,
        ExclusiveDbLogProcessorOptions => DbLogProcessingMode.Exclusive,
        _ => throw new ArgumentOutOfRangeException(nameof(settings))
    };

    protected abstract Task Process(DbShard shard, TDbEntry entry, CancellationToken cancellationToken);

    protected override Task OnRun(DbShard shard, CancellationToken cancellationToken)
        => new AsyncChain($"{nameof(ProcessNewEntries)}[{shard}]", ct => ProcessNewEntries(shard, ct))
            .RetryForever(Settings.RetryDelays, SystemClock, Log)
            .CycleForever()
            .Log(Log)
            .Start(cancellationToken);

    protected virtual Task WhenChanged(DbShard shard, CancellationToken cancellationToken)
        => LogWatcher.WhenChanged(shard, cancellationToken);

    protected virtual async Task ProcessNewEntries(DbShard shard, CancellationToken cancellationToken)
    {
        var timeoutCts = cancellationToken.CreateLinkedTokenSource();
        try {
            var timeoutTask = SystemClock.Delay(Settings.ForcedCheckPeriod.Next(), timeoutCts.Token);
            // WhenEntriesAdded should be invoked before we start reading!
            var whenEntriesAdded = await Task
                .WhenAny(WhenChanged(shard, timeoutCts.Token), timeoutTask)
                .ConfigureAwait(false);
            while (true) { // Reading entries in batches
                var mustContinue = await ProcessBatch(shard, cancellationToken).ConfigureAwait(false);
                if (!mustContinue)
                    break;
            }
            await whenEntriesAdded.ConfigureAwait(false);
        }
        finally {
            // We have to cancel timeoutCts to abort WhenEntriesAdded & timeoutTask
            timeoutCts.CancelAndDisposeSilently();
        }
    }

    protected virtual async Task<bool> ProcessBatch(DbShard shard, CancellationToken cancellationToken)
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
        var entries = await dbContext.Set<TDbEntry>(DbLogProcessor.GetProcessQueryHints(Mode))
            // ReSharper disable once AccessToModifiedClosure
            .Where(o => o.Index >= nextIndex)
            .OrderBy(o => o.Index)
            .Take(batchSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var logLevel = entries.Count == batchSize ? LogLevel.Warning : LogLevel.Debug;
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        Log.IfEnabled(logLevel)?.Log(logLevel,
            $"{nameof(ProcessBatch)}[{{Shard}}]: fetched {{Count}}/{{BatchSize}} entries with Index >= {{LastIndex}}",
            shard.Value, entries.Count, batchSize, nextIndex);

        if (entries.Count == 0)
            return false;

        await GetProcessTasks(shard, entries, nextIndex, cancellationToken)
            .Collect(Settings.ProcessConcurrencyLevel)
            .ConfigureAwait(false);
        nextIndex = entries[^1].Index + 1;

        if (Mode is DbLogProcessingMode.Exclusive) {
            var dbEntries = dbContext.Set<TDbEntry>();
            foreach (var entry in entries) {
                dbEntries.Attach(entry);
                MarkProcessed(dbEntries, entry);
            }
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
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
        if (Mode is DbLogProcessingMode.Cooperative) {
            var minLoggedAt = SystemClock.Now.ToDateTime() - Settings.MaxGapLifespan;
            candidateEntries = candidateEntries.Where(e => e.LoggedAt >= minLoggedAt);
        }
        else
            candidateEntries = candidateEntries.Where(e => !e.IsProcessed);
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
                ProcessGapSafe(shard, nextIndex++, entry.LoggedAt, null);
            yield return ProcessSafe(shard, entry, cancellationToken);
            nextIndex++;
        }
    }

    protected async Task ProcessSafe(DbShard shard, TDbEntry entry, CancellationToken cancellationToken)
    {
        // This method should never fail (unless cancelled)!
        if (entry.IsProcessed)
            return;

        try {
            await Process(shard, entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            ProcessGapSafe(shard, entry.Index, entry.LoggedAt, entry);
        }
    }

    protected void ProcessGapSafe(DbShard shard, long index, Moment loggedAt, TDbEntry? foundEntry)
    {
        // This method should never fail!
        lock (ProcessGapTasks) {
            var key = (shard, index);
            if (ProcessGapTasks.ContainsKey(key))
                return;

            var task = Task.Run(() => ProcessGap(shard, index, loggedAt, foundEntry, StopToken));
            ProcessGapTasks[key] = task;
            _ = task.ContinueWith(_ => {
                lock (ProcessGapTasks)
                    ProcessGapTasks.Remove(key);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    protected async Task ProcessGap(
        DbShard shard, long index, Moment loggedAt, TDbEntry? foundEntry, CancellationToken cancellationToken)
    {
        var tryIndex = 0;
        var lastError = (Exception?)null;
        while (SystemClock.Now < loggedAt + Settings.MaxGapLifespan) {
            tryIndex++;
            await SystemClock.Delay(Settings.ProcessGapDelays[tryIndex], cancellationToken).ConfigureAwait(false);
            try {
                foundEntry ??= await EntryResolver.Get(index, cancellationToken).ConfigureAwait(false);
                if (foundEntry == null)
                    continue;

                if (foundEntry.IsProcessed) {
                    DefaultLog?.Log(Settings.LogLevel,
                        $"{nameof(ProcessGap)}[{{Shard}}]: entry #{{Index}} is already processed by someone else",
                        shard, index);
                    return;
                }

                loggedAt = foundEntry.LoggedAt;
                var isProcessed = await ProcessGapImpl(shard, foundEntry, cancellationToken).ConfigureAwait(false);
                if (isProcessed) {
                    DefaultLog?.Log(Settings.LogLevel,
                        $"{nameof(ProcessGap)}[{{Shard}}]: entry #{{Index}} is processed",
                        shard, index);
                    return;
                }
                foundEntry = null;
            }
            catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                lastError = e;
            }
        }
        var noEntry = lastError == null;
        var logDescription = noEntry ? "no entry" : "failed to process entry";
        var logLevel = noEntry ? LogLevel.Information : LogLevel.Error;
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        Log.IfEnabled(logLevel)?.Log(logLevel, lastError,
            $"{nameof(ProcessGap)}[{{Shard}}]: {logDescription} #{{Index}}",
            shard, index);
    }

    protected async ValueTask<bool> ProcessGapImpl(DbShard shard, TDbEntry foundEntry, CancellationToken cancellationToken)
    {
        if (Mode is DbLogProcessingMode.Cooperative) {
            await Process(shard, foundEntry, cancellationToken).ConfigureAwait(false);
            return true;
        }

        var dbContext = await DbHub.CreateDbContext(shard, readWrite: true, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var _2 = tx.ConfigureAwait(false);
        dbContext.EnableChangeTracking(true);

        var dbEntries = dbContext.Set<TDbEntry>();
        var entry = MemberwiseCloner.Invoke(foundEntry);
        dbEntries.Attach(entry);
        MarkProcessed(dbEntries, entry);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // If we're here, the entry's update x-locked the entry's row
        await Process(shard, foundEntry, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    protected void MarkProcessed(DbSet<TDbEntry> dbEntries, TDbEntry entry)
    {
        entry.Version = DbHub.VersionGenerator.NextVersion(entry.Version);
        entry.IsProcessed = true;
        dbEntries.Update(entry);
    }
}
