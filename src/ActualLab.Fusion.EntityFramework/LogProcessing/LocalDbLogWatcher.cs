using ActualLab.Fusion.EntityFramework.Operations;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public class LocalDbLogWatcher<TDbContext, TDbEntry>(IServiceProvider services)
    : DbLogWatcher<TDbContext, TDbEntry>(services)
    where TDbContext : DbContext
{
    protected override DbShardWatcher CreateShardWatcher(string shard)
        => new ShardWatcher(shard);

    // Nested types

    protected class ShardWatcher(string shard) : DbShardWatcher(shard)
    {
        public override Task NotifyChanged(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MarkChanged();
            return Task.CompletedTask;
        }
    }
}
