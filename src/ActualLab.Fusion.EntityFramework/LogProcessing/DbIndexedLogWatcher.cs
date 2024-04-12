using ActualLab.Fusion.EntityFramework.Operations;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public interface IDbIndexedLogWatcher<TDbContext, TDbEntry>
    where TDbContext : DbContext
    where TDbEntry : class, IDbIndexedLogEntry
{
    Task NotifyChanged(DbShard shard, CancellationToken cancellationToken = default);
    Task WhenChanged(DbShard shard, CancellationToken cancellationToken = default);
}

public abstract class DbIndexedLogWatcher<TDbContext, TDbEntry>(IServiceProvider services)
    : DbWorkerBase<TDbContext>(services), IDbIndexedLogWatcher<TDbContext, TDbEntry>
    where TDbContext : DbContext
    where TDbEntry : class, IDbIndexedLogEntry
{
    protected ConcurrentDictionary<DbShard, LazySlim<DbShard, DbIndexedLogWatcher<TDbContext, TDbEntry>, DbShardWatcher>>
        ShardWatchers { get; set; } = new();

    public abstract Task NotifyChanged(DbShard shard, CancellationToken cancellationToken = default);

    public virtual Task WhenChanged(DbShard shard, CancellationToken cancellationToken = default)
    {
        StopToken.ThrowIfCancellationRequested();
        var watcher = ShardWatchers.GetOrAdd(shard,
            static (shard1, self) => self.CreateShardWatcher(shard1),
            this);
        return watcher.WhenChanged.WaitAsync(cancellationToken);
    }

    // Protected methods

    protected abstract DbShardWatcher CreateShardWatcher(DbShard shard);

    protected override Task OnRun(CancellationToken cancellationToken)
        => TaskExt.NewNeverEndingUnreferenced().WaitAsync(cancellationToken);

    protected override async Task OnStop()
    {
        await ShardWatchers.Values
            .Select(v => v.Value.DisposeAsync().AsTask())
            .Collect()
            .ConfigureAwait(false);
    }
}
