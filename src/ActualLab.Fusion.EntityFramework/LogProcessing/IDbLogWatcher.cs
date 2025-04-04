using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public interface IDbLogWatcher<TDbContext, TDbEntry>
    where TDbContext : DbContext
{
    public Task NotifyChanged(string shard, CancellationToken cancellationToken = default);
    public Task WhenChanged(string shard, CancellationToken cancellationToken = default);
}
