using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Tests;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.Tests.OperationEvents;

public class DbLogWatcherLocalWakeTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task NotifyChangedWakesLocalReader()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        await using var watcher = new CrossHostOnlyDbLogWatcher(services);

        var whenChanged = watcher.WhenChanged("shard0");
        whenChanged.IsCompleted.Should().BeFalse();

        await watcher.NotifyChanged("shard0");
        await whenChanged.WaitAsync(TimeSpan.FromSeconds(1));
        whenChanged.IsCompletedSuccessfully.Should().BeTrue();
    }

    // Nested types

    private sealed class FakeDbContext : DbContext;

    // A watcher whose shard watcher only notifies other hosts (no local wake),
    // mirroring the Redis/Npgsql self-filter that motivated the base-level MarkChanged.
    private sealed class CrossHostOnlyDbLogWatcher(IServiceProvider services)
        : DbLogWatcher<FakeDbContext, object>(services)
    {
        protected override DbShardWatcher CreateShardWatcher(string shard)
            => new ShardWatcher(shard);

        private sealed class ShardWatcher(string shard) : DbShardWatcher(shard)
        {
            public override Task NotifyChanged(CancellationToken cancellationToken)
                => Task.CompletedTask;
        }
    }
}
