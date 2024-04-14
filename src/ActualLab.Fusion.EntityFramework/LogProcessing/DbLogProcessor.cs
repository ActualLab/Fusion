using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public abstract class DbLogProcessor<TDbContext, TDbEntry, TOptions>(
    TOptions settings,
    IServiceProvider services
    ) : DbShardWorkerBase<TDbContext>(services), IDbLogProcessor
    where TDbContext : DbContext
    where TDbEntry : class, IDbLogEntry
    where TOptions : DbLogProcessorOptions
{
    protected IDbLogWatcher<TDbContext, TDbEntry> LogWatcher { get; }
        = services.GetRequiredService<IDbLogWatcher<TDbContext, TDbEntry>>();

    protected IMomentClock SystemClock { get; init; } = services.Clocks().SystemClock;
    protected ILogger? DefaultLog => Log.IfEnabled(Settings.LogLevel);
    protected ILogger? DebugLog => Log.IfEnabled(LogLevel.Debug);

    public TOptions Settings { get; } = settings;
    public abstract DbLogKind LogKind { get; }

    protected abstract Task Process(DbShard shard, TDbEntry entry, CancellationToken cancellationToken);
    protected abstract Task<bool> ProcessBatch(DbShard shard, CancellationToken cancellationToken);

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

    protected async Task<TDbEntry?> GetEntry(TDbContext dbContext, Symbol uuid, CancellationToken cancellationToken)
        => await dbContext.Set<TDbEntry>(LogKind.GetReadOneQueryHints())
            .FirstOrDefaultAsync(x => x.Uuid == uuid.Value, cancellationToken)
            .ConfigureAwait(false);

    protected void UpdateEntryState(DbSet<TDbEntry> dbEntries, TDbEntry entry, LogEntryState state)
    {
        dbEntries.Update(entry);
        entry.State = state;
        entry.Version = DbHub.VersionGenerator.NextVersion(entry.Version);
    }
}
