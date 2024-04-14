using ActualLab.Fusion.EntityFramework.Operations;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public class LocalDbLogWatcher<TDbContext, TDbEntry>(IServiceProvider services)
    : DbLogWatcher<TDbContext, TDbEntry>(services)
    where TDbContext : DbContext
{
    protected override DbShardWatcher CreateShardWatcher(DbShard shard)
        => new ShardWatcher(this, shard);

    // Nested types

    protected class ShardWatcher(LocalDbLogWatcher<TDbContext, TDbEntry> owner, DbShard shard)
        : DbShardWatcher(shard)
    {
        protected LocalDbLogWatcher<TDbContext, TDbEntry> Owner { get; } = owner;

        public override Task NotifyChanged(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MarkChanged();
            return Task.CompletedTask;
        }
    }
}
