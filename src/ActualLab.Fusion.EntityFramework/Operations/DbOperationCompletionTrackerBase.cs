using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public abstract class DbOperationCompletionTrackerBase(IServiceProvider services) : WorkerBase
{
    private ILogger? _log;

    protected IServiceProvider Services { get; } = services;
    protected ConcurrentDictionary<DbShard, DbShardWatcher>? ShardWatchers { get; set; } = new();
    protected ILogger Log => _log ??= Services.LogFor(GetType());

    protected override Task OnRun(CancellationToken cancellationToken)
        => TaskExt.NeverEndingTask.WaitAsync(cancellationToken);

    protected override async Task OnStop()
    {
        if (ShardWatchers == null)
            return;

        var shardWatchers = ShardWatchers;
        ShardWatchers = null;
        await shardWatchers.Values
            .Select(v => v.DisposeAsync().AsTask())
            .Collect()
            .ConfigureAwait(false);
    }

    // Protected methods

    protected abstract DbShardWatcher CreateShardWatcher(DbShard shard);
}

public abstract class DbOperationCompletionTrackerBase<TDbContext, TOptions>(
    TOptions options,
    IServiceProvider services
    ) : DbOperationCompletionTrackerBase(services), IDbOperationLogChangeTracker<TDbContext>
    where TDbContext : DbContext
    where TOptions : DbOperationCompletionTrackingOptions, new()
{
    protected TOptions Options { get; init; } = options;
    protected IDbShardRegistry<TDbContext> ShardRegistry { get; } = services.GetRequiredService<IDbShardRegistry<TDbContext>>();

    public Task WaitForChanges(DbShard shard, CancellationToken cancellationToken = default)
    {
        var shardWatchers = ShardWatchers;
        if (shardWatchers == null)
            return TaskExt.NeverEndingTask.WaitAsync(cancellationToken);

        var watcher = shardWatchers.GetOrAdd(shard,
            static (shard1, self) => self.CreateShardWatcher(shard1),
            this);
        return watcher.WaitForChanges(cancellationToken);
    }
}
