using ActualLab.Fusion.Client;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests;

public class KeyValueServiceWithCacheTest : FusionTestBase
{
    public KeyValueServiceWithCacheTest(ITestOutputHelper @out) : base(@out)
        => UseRemoteComputedCache = true;

    [Fact]
    public async Task BasicTest()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();
        var kv2 = clientServices2.GetRequiredService<IKeyValueService<string>>();
        var smallTimeout = TimeSpan.FromSeconds(1);
        var timeout = TimeSpan.FromSeconds(1);

        await kv.Set("1", "a");

        var c1 = await GetComputed(kv1, "1"); // Nothing is cached yet -> RPC call
        c1.Value.Should().Be("a");
        c1.WhenSynchronized.IsCompleted.Should().BeTrue(); // Cache has 'Get("1")' entry now
        await Assert.ThrowsAnyAsync<TimeoutException>(() =>
            c1.WhenInvalidated().WaitAsync(smallTimeout));

        var c2 = await GetComputed(kv2, "1"); // Cached version fetched
        c2.Value.Should().Be("a");
        c2.WhenSynchronized.IsCompleted.Should().BeFalse();
        await c2.WhenSynchronized(); // Synced
        c2.WhenSynchronized.IsCompleted.Should().BeTrue();
        c2 = await GetComputed(kv2, "1");
        c2.WhenSynchronized.IsCompleted.Should().BeTrue();

        await kv.Set("1", "a");
        await c1.WhenInvalidated().WaitAsync(timeout);
        await c2.WhenInvalidated().WaitAsync(timeout);
    }

    [Theory]
    [InlineData(false, false, 0d)]
    [InlineData(false, true, 0d)]
    [InlineData(true, false, 0d)]
    [InlineData(true, true, 0d)]
    [InlineData(false, false, 3d)]
    [InlineData(false, true, 3d)]
    [InlineData(true, false, 3d)]
    [InlineData(true, true, 3d)]
    [InlineData(false, false, null)]
    [InlineData(false, true, null)]
    [InlineData(true, false, null)]
    [InlineData(true, true, null)]
    public async Task RemoteComputeContextTest(bool waitForConnection, bool syncIfDisconnected, double? timeout)
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();
        var kv2 = clientServices2.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");
        var c1 = await GetComputed(kv1, "1"); // Nothing is cached yet -> RPC call
        c1.Value.Should().Be("a");
        c1.WhenSynchronized.IsCompleted.Should().BeTrue(); // Cache has 'Get("1")' entry now
        await Task.Delay(TimeSpan.FromSeconds(0.1));

        if (waitForConnection)
            await kv2.Set("2", "b");

        var synchronizer = new RemoteComputedSynchronizer() {
            UseWhenDisconnected = syncIfDisconnected
        };
        if (timeout is { } vTimeout)
            synchronizer = synchronizer with {
                TimeoutFactory = vTimeout <= 0
                    ? (_, _) => Task.CompletedTask
                    : (_, ct) => Task.Delay(TimeSpan.FromSeconds(vTimeout), ct)
            };
        var willBeSynchronized = timeout != 0 && (waitForConnection || syncIfDisconnected);
        using (synchronizer.Activate()) {
            var c2 = await GetComputed(kv2, "1");
            c2.Value.Should().Be("a");
            c2.WhenSynchronized.IsCompleted.Should().Be(willBeSynchronized);
        }
    }

    [Fact]
    public async Task StateTest()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();
        var kv2 = clientServices2.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");
        await kv.Set("2", "b");
        var c1 = await GetComputed(kv1, "1");
        c1.WhenSynchronized().IsCompleted.Should().BeTrue();
        var c2 = await GetComputed(kv1, "2");
        c2.WhenSynchronized().IsCompleted.Should().BeTrue();

        var state1 = ClientServices.StateFactory().NewComputed<string>(
            FixedDelayer.Get(0.1),
            (_, ct) => kv2.Get("1", ct));

        var state2 = ClientServices.StateFactory().NewComputed<string>(
            FixedDelayer.Get(0.1),
            (_, ct) => kv2.Get("2", ct));

        var state = ClientServices.StateFactory().NewComputed<string>(
            FixedDelayer.Get(0.5),
            async ct => {
                var s1 = await state1.Use(ct);
                var s2 = await state2.Use(ct);
                return $"{s1} {s2}";
            });

        await state.WhenSynchronized();
        state.Value.Should().Be("a b");

        state1.WhenNonInitial().IsCompleted.Should().BeTrue();
        state2.WhenNonInitial().IsCompleted.Should().BeTrue();
        state.WhenNonInitial().IsCompleted.Should().BeTrue();

        await kv.Set("2", "c");
        state.WhenSynchronized().IsCompleted.Should().BeTrue();
        state.Value.Should().Be("a b");
        await state.When(x => x == "a c").WaitAsync(TimeSpan.FromSeconds(1));
    }

    private static async Task<RemoteComputed<string>> GetComputed(IKeyValueService<string> kv, string key)
    {
        var c = await Computed.Capture(() => kv.Get(key));
        return (RemoteComputed<string>)c;
    }
}
