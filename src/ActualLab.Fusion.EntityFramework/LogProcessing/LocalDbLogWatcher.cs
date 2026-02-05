using ActualLab.Fusion.EntityFramework.Operations;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

/// <summary>
/// A local (in-process) <see cref="IDbLogWatcher{TDbContext, TDbEntry}"/> that immediately
/// notifies watchers on the same host without inter-process communication.
/// </summary>
public class LocalDbLogWatcher<TDbContext, TDbEntry>(IServiceProvider services)
    : DbLogWatcher<TDbContext, TDbEntry>(services)
    where TDbContext : DbContext
{
    protected override DbShardWatcher CreateShardWatcher(string shard)
        => new ShardWatcher(shard);

    // Nested types

    /// <summary>
    /// A <see cref="DbShardWatcher"/> that signals changes in-process by directly
    /// marking the watcher state as changed.
    /// </summary>
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
