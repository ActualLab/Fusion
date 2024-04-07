using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public enum DbLogProcessorMode
{
    Cooperative = 0,
    Exclusive = 1,
}

public record DbLogProcessorOptions
{
    public TimeSpan MaxCommitDuration { get; init; } = TimeSpan.FromSeconds(3);
    public int BatchSize { get; init; } = 128;
    public RandomTimeSpan UnconditionalCheckPeriod { get; init; } = TimeSpan.FromSeconds(5).ToRandom(0.1);
    public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(1, 5);
    public RetryDelaySeq ProcessGapDelays { get; init; } = RetryDelaySeq.Exp(0.25, 1, 0.1, 2);
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
}

public static class DbLogProcessor
{
    public static DbHint[] CooperativeReadHints { get; set; } = DbHintSet.Empty;
    public static DbHint[] ExclusiveReadHints { get; set; } =  DbHintSet.UpdateSkipLocked;

    public static DbHint[] GetProcessQueryHints(DbLogProcessorMode mode)
        => mode is DbLogProcessorMode.Cooperative
            ? CooperativeReadHints
            : ExclusiveReadHints;
}

public abstract class DbLogProcessor<TDbContext, TDbEntry>(DbLogProcessorOptions settings, IServiceProvider services)
    : DbShardWorkerBase<TDbContext>(services)
    where TDbContext : DbContext
    where TDbEntry : class, ILogEntry
{
    protected ConcurrentDictionary<DbShard, long> NextIndexes { get; } = new();
    protected Dictionary<(DbShard Shard, long Index), Task> ProcessGapTasks { get; } = new();

    protected DbLogProcessorOptions Settings { get; } = settings;
    protected IMomentClock Clock { get; init; } = services.Clocks().SystemClock;
    protected DbLogProcessorMode Mode { get; init; } = DbLogProcessorMode.Cooperative;
    protected ILogger? DefaultLog => Log.IfEnabled(Settings.LogLevel);

    protected abstract Task Process(DbShard shard, TDbEntry entry, CancellationToken cancellationToken);
    protected abstract Task WhenEntriesAdded(DbShard shard, CancellationToken cancellationToken);

    protected override Task OnRun(DbShard shard, CancellationToken cancellationToken)
        => new AsyncChain($"{nameof(ProcessNewEntries)}[{shard}]", ct => ProcessNewEntries(shard, ct))
            .RetryForever(Settings.RetryDelays, Clock, Log)
            .CycleForever()
            .Log(Log)
            .Start(cancellationToken);

    protected virtual async Task ProcessNewEntries(DbShard shard, CancellationToken cancellationToken)
    {
        if (!NextIndexes.ContainsKey(shard)) {
            var startEntry = await GetStartEntry(shard, cancellationToken).ConfigureAwait(false);
            if (startEntry != null)
                NextIndexes.TryAdd(shard, startEntry.Index);
        }
        var timeoutCts = cancellationToken.CreateLinkedTokenSource();
        try {
            var timeoutTask = Clock.Delay(Settings.UnconditionalCheckPeriod.Next(), timeoutCts.Token);
            // WhenEntriesAdded should be invoked before we start reading!
            var whenEntriesAdded = await Task
                .WhenAny(WhenEntriesAdded(shard, timeoutCts.Token), timeoutTask)
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

    protected virtual async Task<TDbEntry?> GetStartEntry(DbShard shard, CancellationToken cancellationToken)
    {
        var dbContext = await CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _ = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        var dbLogEntries = dbContext.Set<TDbEntry>().AsQueryable();
        if (Mode is DbLogProcessorMode.Cooperative) {
            var minCommitTime = Clock.Now.ToDateTime() - Settings.MaxCommitDuration;
            dbLogEntries = dbLogEntries.Where(e => e.CommitTime >= minCommitTime);
        }
        return await dbLogEntries
            .OrderBy(e => e.Index)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    protected virtual async Task<bool> ProcessBatch(DbShard shard, CancellationToken cancellationToken)
    {
        if (!NextIndexes.TryGetValue(shard, out var nextIndex))
            return false;

        using var _ = ActivitySource.StartActivity().AddShardTags(shard);
        var dbContext = await CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
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

        var logLevel = entries.Count == batchSize ? LogLevel.Warning : Settings.LogLevel;
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        Log.IfEnabled(logLevel)?.Log(logLevel,
            $"{nameof(ProcessBatch)}[{{Shard}}]: fetched {{Count}}/{{BatchSize}} entries with Index >= {{LastIndex}}",
            shard.Value, entries.Count, batchSize, nextIndex);

        if (entries.Count == 0)
            return false;

        foreach (var entry in entries) {
            if (entry.Index != nextIndex)
                TryProcessGap(shard, entry.Index);
            else
                await TryProcess(shard, entry, cancellationToken).ConfigureAwait(false);
            ++nextIndex;
        }
        if (Mode is DbLogProcessorMode.Exclusive) {
            dbContext.Set<TDbEntry>().RemoveRange(entries);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        NextIndexes[shard] = nextIndex;
        return entries.Count < batchSize;
    }

    protected async Task TryProcess(DbShard shard, TDbEntry entry, CancellationToken cancellationToken)
    {
        try {
            await Process(shard, entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            TryProcessGap(shard, entry.Index);
        }
    }

    protected bool TryProcessGap(DbShard shard, long index)
    {
        lock (ProcessGapTasks) {
            var key = (shard, index);
            if (!ProcessGapTasks.ContainsKey(key))
                return false;

            var task = Task.Run(() => TryProcessGap(shard, index, StopToken));
            ProcessGapTasks[key] = task;
            _ = task.ContinueWith(_ => {
                lock (ProcessGapTasks)
                    ProcessGapTasks.Remove(key);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return true;
        }
    }

    private async Task TryProcessGap(DbShard shard, long index, CancellationToken cancellationToken)
    {
        var tryIndex = 0;
        var endsAt = Clock.Now + Settings.MaxCommitDuration;
        var lastError = (Exception?)null;
        while (Clock.Now < endsAt) {
            tryIndex++;
            await Clock.Delay(Settings.ProcessGapDelays[tryIndex], cancellationToken).ConfigureAwait(false);
            try {
                var isProcessed = await ProcessGap(shard, index, cancellationToken).ConfigureAwait(false);
                if (isProcessed) {
                    DefaultLog?.Log(Settings.LogLevel,
                        $"{nameof(TryProcessGap)}[{{Shard}}]: processed entry with Index = {{Index}}",
                        shard, index);
                    return;
                }
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
            $"{nameof(TryProcessGap)}[{{Shard}}]: {logDescription} with Index = {{Index}}",
            shard, index);
    }

    private async Task<bool> ProcessGap(DbShard shard, long index, CancellationToken cancellationToken)
    {
        var dbContext = await CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        var tx = Mode is DbLogProcessorMode.Exclusive
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        try {
            var entry = await dbContext.Set<TDbEntry>(DbLogProcessor.GetProcessQueryHints(Mode))
                .SingleOrDefaultAsync(o => o.Index == index, cancellationToken)
                .ConfigureAwait(false);
            if (entry == null)
                return false;

            await TryProcess(shard, entry, cancellationToken).ConfigureAwait(false);

            if (tx != null) {
                dbContext.Set<TDbEntry>().RemoveRange(entry);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            return true;
        }
        finally {
            if (tx != null)
                await tx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
