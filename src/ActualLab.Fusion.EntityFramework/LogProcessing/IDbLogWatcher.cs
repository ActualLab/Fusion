using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public interface IDbLogWatcher<TDbContext, TDbEntry>
    where TDbContext : DbContext
{
    public Task NotifyChanged(DbShard shard, CancellationToken cancellationToken = default);
    public Task WhenChanged(DbShard shard, CancellationToken cancellationToken = default);
}
