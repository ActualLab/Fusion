using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Extensions;
using ActualLab.Time.Testing;

namespace ActualLab.Fusion.Tests.Extensions;

public class InMemoryKeyValueStoreTest(ITestOutputHelper @out) : KeyValueStoreTestBase(@out, true);

public class DbKeyValueStoreTest(ITestOutputHelper @out) : KeyValueStoreTestBase(@out, false);

public abstract class KeyValueStoreTestBase : FusionTestBase
{
    protected KeyValueStoreTestBase(ITestOutputHelper @out, bool useInMemoryKeyValueStore) : base(@out)
    {
        UseTestClock = true;
        UseInMemoryKeyValueStore = useInMemoryKeyValueStore;
    }

    [Fact]
    public async Task BasicTest()
    {
        var kvs = Services.GetRequiredService<IKeyValueStore>();
        var shard = DbShard.Single;

        await kvs.Set(shard, "1", "1v");
        (await kvs.Get(shard, "1")).Should().Be("1v");
        await kvs.Remove(shard, "1");
        (await kvs.Get(shard, "1")).Should().Be(null);
    }

    [Fact]
    public async Task ComplexTest()
    {
        var kvs = Services.GetRequiredService<IKeyValueStore>();
        var shard = DbShard.Single;

        await kvs.Set(shard, "1/2", "12");
        (await kvs.Count(shard, "1")).Should().Be(1);
        (await kvs.ListKeySuffixes(shard, "1", 100)).Length.Should().Be(1);
        (await kvs.Count(shard, "1/2")).Should().Be(1);
        (await kvs.ListKeySuffixes(shard, "1/2", 100)).Length.Should().Be(1);
        (await kvs.Count(shard, "1/2/3a")).Should().Be(0);
        (await kvs.ListKeySuffixes(shard, "1/2/3a", 100)).Length.Should().Be(0);

        await kvs.Set(shard, "1/2/3a", "123");
        (await kvs.Count(shard, "1")).Should().Be(2);
        (await kvs.ListKeySuffixes(shard, "1", 100)).Length.Should().Be(2);
        (await kvs.Count(shard, "1/2")).Should().Be(2);
        (await kvs.ListKeySuffixes(shard, "1/2", 100)).Length.Should().Be(2);
        (await kvs.Count(shard, "1/2/3a")).Should().Be(1);
        (await kvs.ListKeySuffixes(shard, "1/2/3a", 100)).Length.Should().Be(1);

        await kvs.Set(shard, "1/2/3b", "123");
        (await kvs.Count(shard, "1")).Should().Be(3);
        (await kvs.ListKeySuffixes(shard, "1", 100)).Length.Should().Be(3);
        (await kvs.Count(shard, "1/2")).Should().Be(3);
        (await kvs.ListKeySuffixes(shard, "1/2", 100)).Length.Should().Be(3);
        (await kvs.Count(shard, "1/2/3a")).Should().Be(1);
        (await kvs.ListKeySuffixes(shard, "1/2/3a", 100)).Length.Should().Be(1);

        (await kvs.ListKeySuffixes(shard, "1", 3))
            .Should().BeEquivalentTo("/2", "/2/3a", "/2/3b");
        (await kvs.ListKeySuffixes(shard, "1", 2))
            .Should().BeEquivalentTo("/2", "/2/3a");
        (await kvs.ListKeySuffixes(shard, "1", PageRef.New(2, "1/2")))
            .Should().BeEquivalentTo("/2/3a", "/2/3b");
        (await kvs.ListKeySuffixes(shard, "1", PageRef.New(2, "1/2/3b"), SortDirection.Descending))
            .Should().BeEquivalentTo("/2/3a", "/2");
        (await kvs.ListKeySuffixes(shard, "1", PageRef.New(1, "1/2/3b"), SortDirection.Descending))
            .Should().BeEquivalentTo("/2/3a");
        (await kvs.ListKeySuffixes(shard, "1", PageRef.New(0, "1/2/3b"), SortDirection.Descending))
            .Should().BeEquivalentTo();

        await kvs.Remove(shard, ["1/2/3c", "1/2/3b"]);
        (await kvs.Count(shard, "1")).Should().Be(2);
        (await kvs.ListKeySuffixes(shard, "1", 100)).Length.Should().Be(2);
        (await kvs.Count(shard, "1/2")).Should().Be(2);
        (await kvs.ListKeySuffixes(shard, "1/2", 100)).Length.Should().Be(2);
        (await kvs.Count(shard, "1/2/3a")).Should().Be(1);
        (await kvs.ListKeySuffixes(shard, "1/2/3a", 100)).Length.Should().Be(1);

        await kvs.Set(shard, [
            ("a/b", "ab", default),
            ("a/c", "ac", default)
        ]);
        (await kvs.Count(shard, "1")).Should().Be(2);
        (await kvs.Count(shard, "a")).Should().Be(2);
        (await kvs.Count(shard, "")).Should().Be(4);
    }

    [Fact]
    public async Task ExpirationTest()
    {
        var kvs = Services.GetRequiredService<IKeyValueStore>();
        var clock = (TestClock)Services.Clocks().SystemClock;
        var shard = DbShard.Single;

        await kvs.Set(shard, "1", "1v", clock.Now + TimeSpan.FromSeconds(5));
        (await kvs.Get(shard, "1")).Should().Be("1v");
        await kvs.Set(shard, "2", "2v", clock.Now + TimeSpan.FromMinutes(10));
        (await kvs.Get(shard, "2")).Should().Be("2v");
        await kvs.Set(shard, "3", "3v");
        (await kvs.Get(shard, "3")).Should().Be("3v");
        await kvs.Set(shard, "4", "4v", clock.Now + TimeSpan.FromMinutes(4.95));
        (await kvs.Get(shard, "4")).Should().Be("4v");

        clock.Settings = new TestClockSettings(TimeSpan.FromMinutes(6));
        await Delay(3); // Let trimmer kick in
        ComputedRegistry.Instance.InvalidateEverything();

        (await kvs.Get(shard, "1")).Should().Be(null);
        (await kvs.Get(shard, "2")).Should().Be("2v");
        (await kvs.Get(shard, "3")).Should().Be("3v");
        (await kvs.Get(shard, "4")).Should().Be(null);
    }
}
