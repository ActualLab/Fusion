using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

/// <summary>
/// Defines the contract for watching database log changes and notifying readers
/// when new entries are available for a specific shard.
/// </summary>
public interface IDbLogWatcher<TDbContext, TDbEntry>
    where TDbContext : DbContext
{
    public Task NotifyChanged(string shard, CancellationToken cancellationToken = default);
    public Task WhenChanged(string shard, CancellationToken cancellationToken = default);
}
