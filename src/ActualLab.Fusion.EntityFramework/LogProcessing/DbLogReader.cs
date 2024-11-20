using System.Diagnostics.CodeAnalysis;
using ActualLab.Resilience;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public abstract class DbLogReader<TDbContext, TDbKey, TDbEntry, TOptions>(
    TOptions settings,
    IServiceProvider services
    ) : DbShardWorkerBase<TDbContext>(services), IDbLogReader
    where TDbContext : DbContext
    where TDbEntry : class, IDbLogEntry
    where TOptions : DbLogReaderOptions
{
    protected Dictionary<(DbShard Shard, TDbKey Key), Task> ReprocessTasks { get; } = new();

    protected IDbLogWatcher<TDbContext, TDbEntry> LogWatcher { get; }
        = services.GetRequiredService<IDbLogWatcher<TDbContext, TDbEntry>>();
    protected MomentClock SystemClock { get; init; } = services.Clocks().SystemClock;
    protected ILogger? DefaultLog => Log.IfEnabled(Settings.LogLevel);
    protected ILogger? DebugLog => Log.IfEnabled(LogLevel.Debug);
    [field: AllowNull, MaybeNull]
    protected RetryLogger ProcessOneRetryLogger => field ??= new RetryLogger(Log, nameof(ProcessOne));

    public TOptions Settings { get; } = settings;
    public abstract DbLogKind LogKind { get; }

    protected abstract Task<TDbEntry?> GetEntry(TDbContext dbContext, TDbKey key, CancellationToken cancellationToken);
    protected abstract Task<int> ProcessBatch(DbShard shard, int batchSize, CancellationToken cancellationToken);
    protected abstract Task<bool> ProcessOne(DbShard shard, TDbKey key, bool mustDiscard, CancellationToken cancellationToken);
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
            var timeoutTask = SystemClock.Delay(Settings.CheckPeriod.Next(), timeoutCts.Token);
            // WhenEntriesAdded should be invoked before we start reading!
            var whenEntriesAdded = await Task
                .WhenAny(WhenChanged(shard, timeoutCts.Token), timeoutTask)
                .ConfigureAwait(false);
            while (true) {
                // Reading entries in batches
                int batchSize;
                while (true) {
                    lock (ReprocessTasks)
                        batchSize = Settings.BatchSize - ReprocessTasks.Count;
                    if (batchSize > 0)
                        break;

                    await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                }
                var entryCount = await ProcessBatch(shard, batchSize, cancellationToken).ConfigureAwait(false);
                if (entryCount < batchSize)
                    break;
            }

            await whenEntriesAdded.ConfigureAwait(false);
        }
        finally {
            // We have to cancel timeoutCts to abort WhenEntriesAdded & timeoutTask
            timeoutCts.CancelAndDisposeSilently();
        }
    }

    protected async Task<bool> ProcessSafe(
        DbShard shard, TDbKey key, TDbEntry entry, bool canReprocess,
        CancellationToken cancellationToken)
    {
        // This method should never fail when canReprocess == true
        if (entry.State != LogEntryState.New)
            return false;

        try {
            await Process(shard, entry, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            if (!canReprocess)
                throw;

            var mustReprocess = Settings.ReprocessPolicy.MustRetry(e, out _);
            var suffix = mustReprocess
                ? ", will reprocess it"
                : ", will discard it";
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            Log.LogError(e,
                $"{nameof(Process)}[{{Shard}}]: failed for entry #{{Key}}{suffix}",
                shard, key);
            ReprocessSafe(shard, key, mustDiscard: !mustReprocess, cancellationToken);
            return false;
        }
    }

    protected void ReprocessSafe(
        DbShard shard, TDbKey key, bool mustDiscard,
        CancellationToken cancellationToken)
    {
        // This method should never fail!
        lock (ReprocessTasks) {
            var fullKey = (shard, key);
            if (ReprocessTasks.ContainsKey(fullKey))
                return;

            var task = Task.Run(() => Reprocess(shard, key, mustDiscard, cancellationToken), CancellationToken.None);
            ReprocessTasks[fullKey] = task;
            _ = task.ContinueWith(_ => {
                lock (ReprocessTasks)
                    ReprocessTasks.Remove(fullKey);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    protected async Task Reprocess(DbShard shard, TDbKey key, bool mustDiscard, CancellationToken cancellationToken)
    {
        for (var i = mustDiscard ? 1 : 0; i < 2; i++) {
            var (mustDiscard1, sProcess, sProcessed, sErrorExtra) = i switch {
                0 => (false, "process", "processed", ", will try to discard it"),
                _ => (true, "discard", "discarded", ""),
            };
            if (mustDiscard1 && LogKind == DbLogKind.Operations)
                return; // No need for discard in operation log

            try {
                await Task.Delay(Settings.ReprocessDelay.Next(), cancellationToken).ConfigureAwait(false);
                var isProcessed = await Settings.ReprocessPolicy
                    .Apply(ct => ProcessOne(shard, key, mustDiscard1, ct), ProcessOneRetryLogger, cancellationToken)
                    .ConfigureAwait(false);
                if (isProcessed) {
                    var logLevel = mustDiscard1 ? LogLevel.Error : Settings.LogLevel;
                    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                    DefaultLog?.Log(logLevel,
                        $"{nameof(Reprocess)}[{{Shard}}]: entry #{{Key}} is {sProcessed}",
                        shard, key);
                    return;
                }

                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                DebugLog?.LogDebug(
                    $"{nameof(Reprocess)}[{{Shard}}]: entry #{{Key}} is processed by another host",
                    shard, key);
                return;
            }
            catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                Log.LogError(e,
                    $"{nameof(Reprocess)}[{{Shard}}]: failed to {sProcess} entry #{{Key}}{sErrorExtra}",
                    shard, key);
            }
        }
    }

    protected void SetEntryState(DbSet<TDbEntry> dbEntries, TDbEntry entry, LogEntryState state)
    {
        dbEntries.Attach(entry);
        entry.State = state;
        entry.Version = DbHub.VersionGenerator.NextVersion(entry.Version);
        dbEntries.Update(entry);
    }
}
