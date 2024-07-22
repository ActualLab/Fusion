using ActualLab.Fusion.EntityFramework.Operations;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public abstract class DbLogWatcher<TDbContext, TDbEntry>(IServiceProvider services)
    : DbWorkerBase<TDbContext>(services), IDbLogWatcher<TDbContext, TDbEntry>
    where TDbContext : DbContext
{
    protected ConcurrentDictionary<DbShard, LazySlim<DbShard, DbLogWatcher<TDbContext, TDbEntry>, DbShardWatcher>>
        ShardWatchers { get; set; } = new();

    public virtual Task NotifyChanged(DbShard shard, CancellationToken cancellationToken = default)
    {
        if (StopToken.IsCancellationRequested)
            return Task.CompletedTask;

        var shardWatcher = GetShardWatcher(shard);
        return shardWatcher.NotifyChanged(cancellationToken);
    }

    public virtual Task WhenChanged(DbShard shard, CancellationToken cancellationToken = default)
    {
        StopToken.ThrowIfCancellationRequested();
        return GetShardWatcher(shard).WhenChanged.WaitAsync(cancellationToken);
    }

    // Protected methods

    protected abstract DbShardWatcher CreateShardWatcher(DbShard shard);

    protected DbShardWatcher GetShardWatcher(DbShard shard) =>
        ShardWatchers.GetOrAdd(shard,
            static (shard1, self) => self.CreateShardWatcher(shard1),
            this);

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
