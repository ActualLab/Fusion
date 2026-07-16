using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Redis;
using ActualLab.Tests.CommandR.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ActualLab.Tests.Audit;

public class PersistenceLifecycleAuditRegressionTest
{
    [Fact]
    public async Task RedisStreamerShouldAdvancePastTheStartMarker()
    {
        var database = new Mock<IDatabase>();
        var subscriber = new Mock<ISubscriber>();
        var multiplexer = new Mock<IConnectionMultiplexer>();
        var startId = (RedisValue)"1-0";
        var sawAdvancedPosition = false;
        database
            .Setup(x => x.StreamReadAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int?>(),
                It.IsAny<CommandFlags>()))
            .Returns<RedisKey, RedisValue, int?, CommandFlags>((_, position, _, _) => {
                if (position == startId)
                    sawAdvancedPosition = true;
                var entries = position == startId
                    ? []
                    : new[] { new StreamEntry(startId, [new NameValueEntry("s", "[")]) };
                return Task.FromResult(entries);
            });
        subscriber
            .Setup(x => x.SubscribeAsync(It.IsAny<RedisChannel>(), It.IsAny<Action<RedisChannel, RedisValue>>(), It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);
        subscriber
            .Setup(x => x.UnsubscribeAsync(It.IsAny<RedisChannel>(), It.IsAny<Action<RedisChannel, RedisValue>>(), It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);
        multiplexer
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        multiplexer
            .Setup(x => x.GetSubscriber(It.IsAny<object>()))
            .Returns(subscriber.Object);
        var connector = new RedisConnector(() => Task.FromResult(multiplexer.Object)) {
            WatchdogTestPeriod = default,
        };
        var streamer = new RedisDb(connector).GetStreamer<int>("stream", new RedisStreamer<int>.Options {
            AppendCheckPeriod = TimeSpan.FromMilliseconds(20),
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var read = async () => await streamer.Read(cts.Token).FirstAsync(cts.Token);

        await read.Should().ThrowAsync<OperationCanceledException>();
        sawAdvancedPosition.Should().BeTrue();
    }

    [Fact]
    public async Task FileSystemLogWatcherShouldDisposeItsNativeWatcher()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddFusion();
        serviceCollection.AddDbContextServices<TestDbContext>();
        var services = serviceCollection.BuildServiceProvider();
        await using var watcher = new TestFileSystemDbLogWatcher(
            FileSystemDbLogWatcherOptions<TestDbContext>.Default,
            services);
        _ = watcher.Run();
        var nativeWatcher = watcher.GetNativeWatcher(DbShard.Single);

        await watcher.DisposeAsync();

        var enable = () => nativeWatcher.EnableRaisingEvents = true;
        enable.Should().Throw<ObjectDisposedException>();
        await services.DisposeAsync();
    }

    [Fact]
    public void ShardDbContextFactoryShouldDisposePerShardProviders()
    {
        var trackers = new ConcurrentDictionary<string, DisposalTracker>();
        var services = new ServiceCollection();
        services.AddFusion();
        services.AddDbContextServices<TestDbContext>(db => db.AddSharding(sharding => {
            sharding.AddShardRegistry("a", "b");
            sharding.AddShardDbContextFactory((_, shard, shardServices) => {
                shardServices.AddSingleton(_ => {
                    var tracker = new DisposalTracker();
                    trackers[shard] = tracker;
                    return tracker;
                });
                shardServices.AddSingleton<IDbContextFactory<TestDbContext>, TrackingDbContextFactory>();
            });
        }));
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IShardDbContextFactory<TestDbContext>>();
        var registry = serviceProvider.GetRequiredService<IDbShardRegistry<TestDbContext>>();
        factory.CreateDbContext("a").Dispose();
        factory.CreateDbContext("b").Dispose();

        registry.Remove("a").Should().BeTrue();
        trackers["a"].IsDisposed.Should().BeTrue();
        trackers["b"].IsDisposed.Should().BeFalse();

        serviceProvider.Dispose();

        trackers["b"].IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task ShardDbContextFactoryShouldDisposeProviderWhenCreationRacesEviction()
    {
        var whenCreating = TaskCompletionSourceExt.New();
        var allowCreation = TaskCompletionSourceExt.New();
        var trackers = new ConcurrentDictionary<string, DisposalTracker>();
        var services = new ServiceCollection();
        services.AddFusion();
        services.AddDbContextServices<TestDbContext>(db => db.AddSharding(sharding => {
            sharding.AddShardRegistry("a");
            sharding.AddShardDbContextFactory((_, shard, shardServices) => {
                whenCreating.TrySetResult();
                allowCreation.Task.GetAwaiter().GetResult();
                shardServices.AddSingleton(_ => {
                    var tracker = new DisposalTracker();
                    trackers[shard] = tracker;
                    return tracker;
                });
                shardServices.AddSingleton<IDbContextFactory<TestDbContext>, TrackingDbContextFactory>();
            });
        }));
        await using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IShardDbContextFactory<TestDbContext>>();
        var registry = serviceProvider.GetRequiredService<IDbShardRegistry<TestDbContext>>();
        var createTask = Task.Run(() => factory.CreateDbContext("a").Dispose());
        await whenCreating.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var removeTask = Task.Run(() => registry.Remove("a"));
        await TestExt.When(
            () => registry.Shards.Value.Should().NotContain("a"),
            TimeSpan.FromSeconds(5));
        allowCreation.TrySetResult();
        (await removeTask.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        var create = async () => await createTask.WaitAsync(TimeSpan.FromSeconds(5));
        await create.Should().ThrowAsync<InvalidOperationException>();

        trackers["a"].IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task ShardDbContextFactoryShouldDisposeProviderWhenCreationRacesRootDisposal()
    {
        var whenCreating = TaskCompletionSourceExt.New();
        var allowCreation = TaskCompletionSourceExt.New();
        var trackers = new ConcurrentDictionary<string, DisposalTracker>();
        var services = new ServiceCollection();
        services.AddFusion();
        services.AddDbContextServices<TestDbContext>(db => db.AddSharding(sharding => {
            sharding.AddShardRegistry("a", "b");
            sharding.AddShardDbContextFactory((_, shard, shardServices) => {
                if (shard == "a") {
                    whenCreating.TrySetResult();
                    allowCreation.Task.GetAwaiter().GetResult();
                }
                shardServices.AddSingleton(_ => {
                    var tracker = new DisposalTracker();
                    trackers[shard] = tracker;
                    return tracker;
                });
                shardServices.AddSingleton<IDbContextFactory<TestDbContext>, TrackingDbContextFactory>();
            });
        }));
        await using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IShardDbContextFactory<TestDbContext>>();
        var createTask = Task.Run(() => factory.CreateDbContext("a").Dispose());
        await whenCreating.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var disposeTask = Task.Run(() => ((IDisposable)factory).Dispose());
        await TestExt.When(() => {
            var create = () => factory.CreateDbContext("b").Dispose();
            create.Should().Throw<ObjectDisposedException>();
        }, TimeSpan.FromSeconds(5));
        allowCreation.TrySetResult();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
        await createTask.SilentAwait(false);

        trackers["a"].IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void ShardDbContextFactoryShouldNotDisposeExternalFactories()
    {
        var externalFactory = new DisposableDbContextFactory();
        var services = new ServiceCollection();
        services.AddFusion();
        services.AddDbContextServices<TestDbContext>(db => db.AddSharding(sharding => {
            sharding.AddShardRegistry("a");
            sharding.AddShardDbContextFactory(_ => (_, _) => externalFactory);
        }));
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IShardDbContextFactory<TestDbContext>>();
        var registry = serviceProvider.GetRequiredService<IDbShardRegistry<TestDbContext>>();
        factory.CreateDbContext("a").Dispose();

        registry.Remove("a").Should().BeTrue();
        externalFactory.DisposeCount.Should().Be(0);

        serviceProvider.Dispose();
        externalFactory.DisposeCount.Should().Be(0);
    }

    [Fact]
    public void ShardDbContextFactoryShouldDisposeProviderWhenFactoryResolutionFails()
    {
        DisposalTracker? tracker = null;
        var services = new ServiceCollection();
        services.AddFusion();
        services.AddDbContextServices<TestDbContext>(db => db.AddSharding(sharding => {
            sharding.AddShardRegistry("a");
            sharding.AddShardDbContextFactory((_, _, shardServices) => {
                shardServices.AddSingleton<DisposalTracker>();
                shardServices.AddSingleton<IDbContextFactory<TestDbContext>>(c => {
                    tracker = c.GetRequiredService<DisposalTracker>();
                    throw new InvalidOperationException("Factory resolution failed.");
                });
            });
        }));
        using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IShardDbContextFactory<TestDbContext>>();

        var create = () => factory.CreateDbContext("a").Dispose();
        create.Should().Throw<InvalidOperationException>();

        tracker.Should().NotBeNull();
        tracker!.IsDisposed.Should().BeTrue();
    }

    private sealed class TestFileSystemDbLogWatcher(
        FileSystemDbLogWatcherOptions<TestDbContext> settings,
        IServiceProvider services)
        : FileSystemDbLogWatcher<TestDbContext, DbOperation>(settings, services)
    {
        public FileSystemWatcher GetNativeWatcher(string shard)
            => ((ShardWatcher)GetShardWatcher(shard)).Watcher;
    }

    private sealed class TrackingDbContextFactory(DisposalTracker tracker) : IDbContextFactory<TestDbContext>
    {
        private DisposalTracker Tracker { get; } = tracker;

        public TestDbContext CreateDbContext()
        {
            Tracker.IsDisposed.Should().BeFalse();
            return new TestDbContext(new DbContextOptions<TestDbContext>());
        }
    }

    private sealed class DisposableDbContextFactory : IDbContextFactory<TestDbContext>, IDisposable
    {
        public int DisposeCount { get; private set; }

        public TestDbContext CreateDbContext()
            => new(new DbContextOptions<TestDbContext>());

        public void Dispose()
            => DisposeCount++;
    }

    private sealed class DisposalTracker : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
            => IsDisposed = true;
    }
}
