using ActualLab.Fusion.EntityFramework.Operations;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public interface IDbLogWatcher<TDbContext, TDbEntry>
    where TDbContext : DbContext
    where TDbEntry : class, ILogEntry
{
    Task Notify(DbShard shard, CancellationToken cancellationToken = default);
    Task WhenChanged(DbShard shard, CancellationToken cancellationToken = default);
}

public abstract class DbLogWatcher<TDbContext, TDbEntry>(IServiceProvider services)
    : DbWorkerBase<TDbContext>(services), IDbLogWatcher<TDbContext, TDbEntry>
    where TDbContext : DbContext
    where TDbEntry : class, ILogEntry
{
    protected ConcurrentDictionary<DbShard, LazySlim<DbShard, DbLogWatcher<TDbContext, TDbEntry>, DbShardWatcher>>
        ShardWatchers { get; set; } = new();

    public abstract Task Notify(DbShard shard, CancellationToken cancellationToken = default);

    public virtual Task WhenChanged(DbShard shard, CancellationToken cancellationToken = default)
    {
        if (StopToken.IsCancellationRequested)
            return TaskExt.NeverEndingTask.WaitAsync(cancellationToken);

        var watcher = ShardWatchers.GetOrAdd(shard,
            static (shard1, self) => self.CreateShardWatcher(shard1),
            this);
        return watcher.WhenChanged.WaitAsync(cancellationToken);
    }

    // Protected methods

    protected abstract DbShardWatcher CreateShardWatcher(DbShard shard);

    protected override Task OnRun(CancellationToken cancellationToken)
        => TaskExt.NeverEndingTask.WaitAsync(cancellationToken);

    protected override async Task OnStop()
    {
        await ShardWatchers.Values
            .Select(v => v.Value.DisposeAsync().AsTask())
            .Collect()
            .ConfigureAwait(false);
    }
}
