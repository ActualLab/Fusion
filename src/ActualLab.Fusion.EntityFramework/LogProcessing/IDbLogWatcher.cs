using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public interface IDbLogWatcher<TDbContext, TDbEntry>
    where TDbContext : DbContext
{
    Task NotifyChanged(DbShard shard, CancellationToken cancellationToken = default);
    Task WhenChanged(DbShard shard, CancellationToken cancellationToken = default);
}
