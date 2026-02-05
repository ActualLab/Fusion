using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

/// <summary>
/// A no-op <see cref="IDbLogWatcher{TDbContext, TDbEntry}"/> placeholder that logs a warning
/// about missing watcher configuration. Change notifications rely on periodic polling.
/// </summary>
public class FakeDbLogWatcher<TDbContext, TDbEntry>
    : DbServiceBase<TDbContext>, IDbLogWatcher<TDbContext, TDbEntry>
    where TDbContext : DbContext
{
    public FakeDbLogWatcher(IServiceProvider services) : base(services)
    {
        var watcherType = typeof(IDbLogWatcher<TDbContext, TDbEntry>);
        Log.LogWarning(
            "{DbLogWatcherType} is not configured, so no change notifications are sent & received for this log!",
            watcherType.GetName());
    }

    public Task NotifyChanged(string shard, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task WhenChanged(string shard, CancellationToken cancellationToken = default)
        => TaskExt.NeverEnding(cancellationToken);
}
