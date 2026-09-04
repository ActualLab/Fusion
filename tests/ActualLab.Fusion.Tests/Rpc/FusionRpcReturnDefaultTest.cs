using ActualLab.Fusion.Client;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Rpc;
using ActualLab.Rpc.Middlewares;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class FusionRpcReturnDefaultTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private volatile int _inboundCallDelayMs;
    private bool _useRemoteComputedCache = true;
    private CountingRemoteComputedCache? _cache;

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var fusion = services.AddFusion();
        fusion.AddServerAndClient<IReturnDefaultTester, ReturnDefaultTester>();
        services.AddRpc().AddMiddleware(_ => new RpcInboundCallDelayer() {
            DelayProvider = _ => TimeSpan.FromMilliseconds(_inboundCallDelayMs),
        });
        if (_useRemoteComputedCache)
            services.AddSingleton<IRemoteComputedCache>(c => _cache = new CountingRemoteComputedCache(
                new InMemoryRemoteComputedCache(InMemoryRemoteComputedCache.Options.Default, c)));
    }

    [Fact]
    public async Task FirstCallServesDefaultThenRealValueTest()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IReturnDefaultTester>();

        _inboundCallDelayMs = 1000;
        var sw = Stopwatch.StartNew();
        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetDefault("1"));
        sw.ElapsedMilliseconds.Should().BeLessThan(500, "the default must not wait for the server");
        // The server stays slow until every "not yet synchronized" assertion below is made
        c1.Value.Should().BeNull();
        c1.IsConsistent().Should().BeTrue();
        c1.WhenSynchronized.IsCompleted.Should().BeFalse();
        c1.IsSynchronized(ComputedSynchronizer.Precise.Instance).Should().BeFalse();
        c1.CacheEntry.Should().NotBeNull();
        c1.CacheEntry!.DeserializedValue.Should().BeNull();
        c1.Options.RemoteComputedCacheMode.Should().Be(RemoteComputedCacheMode.ReturnDefault);
        c1.Options.MinCacheDuration.Should().Be(ComputedOptions.ClientDefault.MinCacheDuration);

        _inboundCallDelayMs = 0;
        await c1.WhenSynchronized.WaitAsync(Timeout);
        c1.IsInvalidated().Should().BeTrue("the real value must displace the default");
        var c2 = (RemoteComputed<string>)Computed.GetExisting(() => client.GetDefault("1"))!;
        c2.Value.Should().Be("v-1");
        c2.IsConsistent().Should().BeTrue();
        c2.WhenSynchronized.IsCompleted.Should().BeTrue();
        c2.IsSynchronized(ComputedSynchronizer.Precise.Instance).Should().BeTrue();
        c2.CacheEntry!.DeserializedValue.Should().BeNull("the entry must stay the default one");
    }

    [Fact]
    public async Task ValueTypeDefaultTest()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IReturnDefaultTester>();

        var c1 = (RemoteComputed<int>)await Computed.Capture(() => client.GetDefaultLength("1"));
        c1.Value.Should().Be(0);
        c1.WhenSynchronized.IsCompleted.Should().BeFalse();

        await c1.WhenSynchronized.WaitAsync(Timeout);
        var c2 = (RemoteComputed<int>)Computed.GetExisting(() => client.GetDefaultLength("1"))!;
        c2.Value.Should().Be("v-1".Length);
        c2.WhenSynchronized.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task DisconnectedServesDefaultTest()
    {
        using var listener = new StaleValueListener();
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IReturnDefaultTester>();
        var server = services.GetRequiredService<ReturnDefaultTester>();

        var c1 = await GetSynchronized(client, "1");
        c1.Value.Should().Be("v-1");

        await connection.Disconnect();
        server.Set("1", "b");
        c1.Invalidate();
        var c2 = (RemoteComputed<string>)await c1.Update();
        c2.Value.Should().BeNull("a disconnected ReturnDefault method re-serves the default");
        c2.IsConsistent().Should().BeTrue();
        c2.WhenSynchronized.IsCompleted.Should().BeFalse();
        // The stale path is reachable only because c1 stayed pseudo-registered with its default entry
        listener.Operations.Should().BeEquivalentTo("connection_check");

        await connection.Connect();
        await c2.WhenInvalidated().WaitAsync(Timeout);
        var c3 = (RemoteComputed<string>)await c2.Update();
        c3.Value.Should().Be("b");
        c3.IsConsistent().Should().BeTrue();
        await c3.WhenSynchronized.WaitAsync(Timeout);
        await c2.WhenSynchronized.WaitAsync(Timeout);
    }

    [Fact]
    public async Task CacheFallbackWinsOverConnectTimeoutTest()
    {
        using var listener = new StaleValueListener();
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IReturnDefaultTester>();
        var server = services.GetRequiredService<ReturnDefaultTester>();

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetDefaultWithConnectTimeout("1"));
        await c1.WhenSynchronized.WaitAsync(Timeout);
        c1 = (RemoteComputed<string>)Computed.GetExisting(() => client.GetDefaultWithConnectTimeout("1"))!;
        c1.Value.Should().Be("v-1");

        // Mid-call disconnect: the fallback fires at once (CacheFallbackDelay = 0) and marks the call
        // served, so the 1s ConnectTimeout must never abort it - it is what validates the served value.
        server.Set("1", "b");
        c1.Invalidate();
        _inboundCallDelayMs = 1000;
        var updateTask = c1.Update();
        await Delay(0.3);
        await connection.Disconnect();
        var c2 = (RemoteComputed<string>)await updateTask;
        c2.Value.Should().BeNull("the default is served while the peer is away");
        c2.Error.Should().BeNull();
        listener.Operations.Should().BeEquivalentTo("active_call");

        // Well past ConnectTimeout, and the call is still alive
        await Delay(1.5);
        c2.WhenSynchronized.IsCompleted.Should().BeFalse("a served call must outlive ConnectTimeout");
        c2.IsConsistent().Should().BeTrue();

        _inboundCallDelayMs = 0;
        await connection.Connect();
        await c2.WhenInvalidated().WaitAsync(Timeout);
        var c3 = (RemoteComputed<string>)await c2.Update();
        c3.Value.Should().Be("b", "the resent call must still validate the served value");
        await c3.WhenSynchronized.WaitAsync(Timeout);
        await c2.WhenSynchronized.WaitAsync(Timeout);
    }

    [Fact]
    public async Task MidCallDisconnectServesDefaultTest()
    {
        using var listener = new StaleValueListener();
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IReturnDefaultTester>();

        var c1 = await GetSynchronized(client, "1");
        c1.Invalidate();
        _inboundCallDelayMs = 1000;
        var updateTask = c1.Update();
        await Delay(0.3);
        await connection.Disconnect(); // Mid-call disconnect -> the send/disconnect race branch
        var c2 = (RemoteComputed<string>)await updateTask;
        c2.Value.Should().BeNull();
        c2.WhenSynchronized.IsCompleted.Should().BeFalse();
        listener.Operations.Should().BeEquivalentTo("active_call");

        _inboundCallDelayMs = 0;
        await connection.Connect();
        await c2.WhenInvalidated().WaitAsync(Timeout);
        var c3 = (RemoteComputed<string>)await c2.Update();
        c3.Value.Should().Be("v-1");
        await c3.WhenSynchronized.WaitAsync(Timeout);
        await c2.WhenSynchronized.WaitAsync(Timeout);
    }

    [Fact]
    public async Task RegisteredCacheIsNeverTouchedTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IReturnDefaultTester>();
        var cache = _cache!;
        await cache.WhenInitialized;

        var c1 = await GetSynchronized(client, "1");
        var g1 = (RemoteComputed<string>)await Computed.Capture(() => client.Get("1"));
        g1.WhenSynchronized.IsCompleted.Should().BeTrue();

        await connection.Disconnect();
        c1.Invalidate();
        var c2 = (RemoteComputed<string>)await c1.Update();
        c2.Value.Should().BeNull();
        g1.Invalidate();
        var g2 = (RemoteComputed<string>)await g1.Update();
        g2.Value.Should().Be("v-1");

        await connection.Connect();
        // ReturnDefault: the real value displaces the default; Cache: the server confirms the stale value in place
        await c2.WhenSynchronized.WaitAsync(Timeout);
        c2.IsInvalidated().Should().BeTrue();
        var c3 = (RemoteComputed<string>)Computed.GetExisting(() => client.GetDefault("1"))!;
        c3.Value.Should().Be("v-1");
        await g2.WhenSynchronized.WaitAsync(Timeout);
        g2.IsConsistent().Should().BeTrue();

        cache.GetCount(".Get:", "Get").Should().BeGreaterThan(0);
        cache.GetCount(".Get:", "Set").Should().BeGreaterThan(0);
        cache.GetCount("GetDefault", "Get").Should().Be(0);
        cache.GetCount("GetDefault", "Set").Should().Be(0);
        cache.GetCount("GetDefault", "Remove").Should().Be(0);
    }

    [Fact]
    public async Task WorksWithoutRegisteredCacheTest()
    {
        _useRemoteComputedCache = false;
        await using var services = CreateServices();
        services.GetService<IRemoteComputedCache>().Should().BeNull();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IReturnDefaultTester>();

        var g1 = (RemoteComputed<string>)await Computed.Capture(() => client.Get("1"));
        g1.Options.RemoteComputedCacheMode.Should().Be(RemoteComputedCacheMode.NoCache, "Cache needs a store");
        g1.Value.Should().Be("v-1");
        g1.WhenSynchronized.IsCompleted.Should().BeTrue();
        g1.CacheEntry.Should().BeNull();

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetDefault("1"));
        c1.Options.RemoteComputedCacheMode.Should().Be(RemoteComputedCacheMode.ReturnDefault);
        c1.Value.Should().BeNull();
        c1.WhenSynchronized.IsCompleted.Should().BeFalse();
        await c1.WhenSynchronized.WaitAsync(Timeout);
        var c2 = (RemoteComputed<string>)Computed.GetExisting(() => client.GetDefault("1"))!;
        c2.Value.Should().Be("v-1");

        await connection.Disconnect();
        c2.Invalidate();
        var c3 = (RemoteComputed<string>)await c2.Update();
        c3.Value.Should().BeNull();
        c3.WhenSynchronized.IsCompleted.Should().BeFalse();

        await connection.Connect();
        await c3.WhenInvalidated().WaitAsync(Timeout);
        var c4 = (RemoteComputed<string>)await c3.Update();
        c4.Value.Should().Be("v-1");
        await c3.WhenSynchronized.WaitAsync(Timeout);
    }

    private static async Task<RemoteComputed<string>> GetSynchronized(IReturnDefaultTester client, string key)
    {
        var computed = (RemoteComputed<string>)await Computed.Capture(() => client.GetDefault(key));
        await computed.WhenSynchronized.WaitAsync(Timeout);
        computed = (RemoteComputed<string>)Computed.GetExisting(() => client.GetDefault(key))!;
        computed.WhenSynchronized.IsCompleted.Should().BeTrue();
        return computed;
    }
}
