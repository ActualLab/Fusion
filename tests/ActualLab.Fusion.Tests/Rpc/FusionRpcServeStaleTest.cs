using System.Diagnostics.Metrics;
using ActualLab.Fusion.Client;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Rpc;
using ActualLab.Rpc.Middlewares;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;
using ActualLab.Tests.Rpc;

namespace ActualLab.Fusion.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class FusionRpcServeStaleTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private volatile int _inboundCallDelayMs;
    private ParkingReconnectDelayer? _reconnectDelayer;

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var fusion = services.AddFusion();
        fusion.AddServerAndClient<IServeStaleTester, ServeStaleTester>();
        services.AddRpc().AddMiddleware(_ => new RpcInboundCallDelayer() {
            DelayProvider = _ => TimeSpan.FromMilliseconds(_inboundCallDelayMs),
        });
        services.AddSingleton<IRemoteComputedCache>(
            c => new InMemoryRemoteComputedCache(InMemoryRemoteComputedCache.Options.Default, c));
        // Reconnect attempts come well within the 1s timeouts used below, unless parked
        services.AddSingleton<RpcClientPeerReconnectDelayer>(c => _reconnectDelayer = new ParkingReconnectDelayer(c) {
            Delays = RetryDelaySeq.Fixed(0.25),
        });
    }

    [Fact]
    public async Task SupersededStaleComputedMustSynchronizeTest()
    {
        var operations = new ConcurrentQueue<string>();
        using var listener = StartStaleValueListener(operations);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IServeStaleTester>();

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.Get("1"));
        c1.Value.Should().Be("v-1");
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        await connection.Disconnect();
        c1.Invalidate();
        var c2 = (RemoteComputed<string>)await c1.Update(); // Disconnected + cached value -> serve-stale
        c2.Value.Should().Be("v-1");
        c2.WhenSynchronized.IsCompleted.Should().BeFalse();

        c2.Invalidate();
        var c3 = (RemoteComputed<string>)await c2.Update(); // Serve-stale again, c2 is superseded
        c3.WhenSynchronized.IsCompleted.Should().BeFalse();

        await connection.Connect();
        await c3.WhenSynchronized.WaitAsync(Timeout); // The update sent on reconnect gets "match"
        c3.IsConsistent().Should().BeTrue("a confirmed stale value must stay in place");

        // Every superseded computed must synchronize once its successor does -
        // otherwise ComputedSynchronizer.Precise waits on it forever
        await c2.WhenSynchronized.WaitAsync(Timeout);
        operations.Should().BeEquivalentTo("connection_check", "connection_check");
    }

    [Fact]
    public async Task MidCallDisconnectStaleComputedMustSynchronizeTest()
    {
        var operations = new ConcurrentQueue<string>();
        using var listener = StartStaleValueListener(operations);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IServeStaleTester>();

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.Get("1"));
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        await connection.Disconnect();
        c1.Invalidate();
        var c2 = (RemoteComputed<string>)await c1.Update(); // Disconnected + cached value -> serve-stale
        c2.WhenSynchronized.IsCompleted.Should().BeFalse();

        await connection.Connect();
        await c2.WhenSynchronized.WaitAsync(Timeout);
        c2.IsConsistent().Should().BeTrue();

        c2.Invalidate();
        _inboundCallDelayMs = 1000;
        var updateTask = c2.Update();
        await Delay(0.3);
        await connection.Disconnect(); // Mid-call disconnect -> the send/disconnect race branch
        var c3 = (RemoteComputed<string>)await updateTask;
        c3.WhenSynchronized.IsCompleted.Should().BeFalse();

        _inboundCallDelayMs = 0;
        await connection.Connect();
        await c3.WhenSynchronized.WaitAsync(Timeout); // The resent call gets "match"
        c3.IsConsistent().Should().BeTrue();

        // Every superseded computed must synchronize once its successor does -
        // otherwise ComputedSynchronizer.Precise waits on it forever
        await c2.WhenSynchronized.WaitAsync(Timeout);
        operations.Should().BeEquivalentTo("connection_check", "active_call");
    }

    [Fact]
    public async Task ReconnectWithinTimeoutMidCallYieldsFreshValueTest()
    {
        var operations = new ConcurrentQueue<string>();
        using var listener = StartStaleValueListener(operations);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IServeStaleTester>();

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetWithCacheFallbackDelay("1"));
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        c1.Invalidate();
        _inboundCallDelayMs = 1000;
        var updateTask = c1.Update();
        await Delay(0.3);
        await connection.Disconnect();
        await Delay(0.3);
        await connection.Connect(); // Within CacheFallbackDelay (1s)
        var c2 = (RemoteComputed<string>)await updateTask;
        c2.Value.Should().Be("v-1");
        c2.IsConsistent().Should().BeTrue();
        c2.WhenSynchronized.IsCompleted.Should().BeTrue("the resent call must complete the update");
        operations.Should().BeEmpty();
    }

    [Fact]
    public async Task NoReconnectMidCallServesStaleAfterTimeoutTest()
    {
        var operations = new ConcurrentQueue<string>();
        using var listener = StartStaleValueListener(operations);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<IServeStaleTester>();

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetWithCacheFallbackDelay("1"));
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        c1.Invalidate();
        _inboundCallDelayMs = 1000;
        var sw = Stopwatch.StartNew();
        var updateTask = c1.Update();
        await Delay(0.3);
        await connection.Disconnect();
        var c2 = (RemoteComputed<string>)await updateTask;
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(1.2), "CacheFallbackDelay must run out first");
        c2.Value.Should().Be("v-1");
        c2.IsConsistent().Should().BeTrue();
        c2.WhenSynchronized.IsCompleted.Should().BeFalse();
        operations.Should().BeEquivalentTo("active_call");
        var pendingCall = clientPeer.OutboundCalls.Single();
        pendingCall.ResultTask.IsCompleted.Should().BeFalse("the call must stay registered for the resend");

        _inboundCallDelayMs = 0;
        await connection.Connect();
        await c2.WhenSynchronized.WaitAsync(Timeout);
        c2.IsConsistent().Should().BeTrue("the server confirmed the cached value");
        (await c2.WhenCallBound).Should().BeSameAs(pendingCall, "the pending call must be reused, not reissued");
        await c1.WhenSynchronized.WaitAsync(Timeout);
    }

    [Fact]
    public async Task NotConnectedAtCallTimeServesStaleAfterTimeoutTest()
    {
        var operations = new ConcurrentQueue<string>();
        using var listener = StartStaleValueListener(operations);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IServeStaleTester>();
        var server = services.GetRequiredService<ServeStaleTester>();

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetWithCacheFallbackDelay("1"));
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        await connection.Disconnect();
        server.Set("1", "b"); // Changes while the client is offline
        c1.Invalidate();
        var sw = Stopwatch.StartNew();
        var c2 = (RemoteComputed<string>)await c1.Update();
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(0.9), "CacheFallbackDelay must run out first");
        c2.Value.Should().Be("v-1");
        c2.WhenSynchronized.IsCompleted.Should().BeFalse();
        operations.Should().BeEquivalentTo("connection_check");

        await connection.Connect();
        await c2.WhenSynchronized.WaitAsync(Timeout);
        c2.IsInvalidated().Should().BeTrue("the fresh value must displace the stale one");
        var c3 = (RemoteComputed<string>)Computed.GetExisting(() => client.GetWithCacheFallbackDelay("1"))!;
        c3.Value.Should().Be("b");
        c3.IsConsistent().Should().BeTrue();
        c3.WhenSynchronized.IsCompleted.Should().BeTrue();
        await c1.WhenSynchronized.WaitAsync(Timeout);
    }

    [Fact]
    public async Task ReconnectWithinTimeoutAtCallTimeYieldsFreshValueTest()
    {
        var operations = new ConcurrentQueue<string>();
        using var listener = StartStaleValueListener(operations);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IServeStaleTester>();
        var server = services.GetRequiredService<ServeStaleTester>();

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetWithCacheFallbackDelay("1"));
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        await connection.Disconnect();
        server.Set("1", "b");
        c1.Invalidate();
        var updateTask = c1.Update();
        await Delay(0.3);
        await connection.Connect(); // Within CacheFallbackDelay (1s)
        var c2 = (RemoteComputed<string>)await updateTask;
        c2.Value.Should().Be("b");
        c2.IsConsistent().Should().BeTrue();
        c2.WhenSynchronized.IsCompleted.Should().BeTrue();
        operations.Should().BeEmpty();
    }

    [Fact]
    public async Task ParkedReconnectServesStaleImmediatelyTest()
    {
        var operations = new ConcurrentQueue<string>();
        using var listener = StartStaleValueListener(operations);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IServeStaleTester>();

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetWithCacheFallbackDelay("1"));
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        _reconnectDelayer!.ParkDelay = TimeSpan.FromMinutes(1); // As if the OS reported "offline"
        await connection.Disconnect();
        c1.Invalidate();
        var sw = Stopwatch.StartNew();
        var c2 = (RemoteComputed<string>)await c1.Update();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(0.5), "a reconnect parked past CacheFallbackDelay is not waited for");
        c2.Value.Should().Be("v-1");
        c2.WhenSynchronized.IsCompleted.Should().BeFalse();
        operations.Should().BeEquivalentTo("connection_check");

        _reconnectDelayer.ParkDelay = null;
        _reconnectDelayer.CancelDelays();
        await connection.Connect();
        await c2.WhenSynchronized.WaitAsync(Timeout);
        c2.IsConsistent().Should().BeTrue();
    }

    [Fact]
    public async Task ColdMissWithoutConnectTimeoutKeepsWaitingTest()
    {
        var operations = new ConcurrentQueue<string>();
        using var listener = StartStaleValueListener(operations);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IServeStaleTester>();

        await connection.Disconnect();
        var getTask = client.GetNoCache("1"); // Nothing to fall back to, ConnectTimeout = inf
        await Delay(1.5);
        getTask.IsCompleted.Should().BeFalse();
        await connection.Connect();
        (await getTask.WaitAsync(Timeout)).Should().Be("v-1");

        // Same for a mid-call disconnect
        var c1 = (RemoteComputed<string>)Computed.GetExisting(() => client.GetNoCache("1"))!;
        c1.Invalidate();
        _inboundCallDelayMs = 1000;
        var updateTask = c1.Update().AsTask();
        await Delay(0.3);
        await connection.Disconnect();
        await Delay(1.5);
        updateTask.IsCompleted.Should().BeFalse();

        _inboundCallDelayMs = 0;
        await connection.Connect();
        var c2 = (RemoteComputed<string>)await updateTask.WaitAsync(Timeout);
        c2.Value.Should().Be("v-1");
        c2.WhenSynchronized.IsCompleted.Should().BeTrue();
        operations.Should().BeEmpty();
    }

    [Fact]
    public async Task NoCacheFallsThroughToConnectTimeoutTest()
    {
        var operations = new ConcurrentQueue<string>();
        using var listener = StartStaleValueListener(operations);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IServeStaleTester>();

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetNoCacheWithBothTimeouts("1"));
        c1.Value.Should().Be("v-1");

        // Mid-call disconnect on a method with both a CacheFallbackDelay (0.3s) and a ConnectTimeout (1s).
        // There is no cache entry, so the fallback handler declines and ConnectTimeout aborts the call.
        c1.Invalidate();
        _inboundCallDelayMs = 1000;
        var sw = Stopwatch.StartNew();
        var updateTask = c1.Update().AsTask();
        await Delay(0.3);
        await connection.Disconnect();
        var c2 = (RemoteComputed<string>)await updateTask.WaitAsync(Timeout);
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(1.2),
            "the declined fallback must not shorten ConnectTimeout");
        c2.Error.Should().BeOfType<RpcTimeoutException>().Which.TimeoutKind.Should().Be(RpcTimeoutKind.Connect);
        operations.Should().BeEmpty("nothing was served");

        _inboundCallDelayMs = 0;
        await connection.Connect();
        var c3 = (RemoteComputed<string>)await c2.Update();
        c3.Value.Should().Be("v-1");
        c3.WhenSynchronized.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task ColdMissFailsAfterConnectTimeoutTest()
    {
        var operations = new ConcurrentQueue<string>();
        using var listener = StartStaleValueListener(operations);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IServeStaleTester>();

        // Nothing to fall back to: the call fails like any other RPC call would
        await connection.Disconnect();
        var sw = Stopwatch.StartNew();
        var timeout = await Assert.ThrowsAsync<RpcTimeoutException>(() => client.GetNoCacheWithConnectTimeout("1"));
        timeout.TimeoutKind.Should().Be(RpcTimeoutKind.Connect);
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(0.9), "ConnectTimeout must run out first");
        await connection.Connect();
        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetNoCacheWithConnectTimeout("1"));
        c1.Value.Should().Be("v-1");

        // Same for a mid-call disconnect: nothing to fall back to, so the in-flight call
        // is aborted once ConnectTimeout runs out, and the computed carries its error
        c1.Invalidate();
        _inboundCallDelayMs = 1000;
        sw.Restart();
        var updateTask = c1.Update().AsTask();
        await Delay(0.3);
        await connection.Disconnect();
        var c2 = (RemoteComputed<string>)await updateTask.WaitAsync(Timeout);
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(1.2), "ConnectTimeout must run out first");
        c2.Error.Should().BeOfType<RpcTimeoutException>().Which.TimeoutKind.Should().Be(RpcTimeoutKind.Connect);

        _inboundCallDelayMs = 0;
        await connection.Connect();
        var c3 = (RemoteComputed<string>)await c2.Update();
        c3.Value.Should().Be("v-1");
        c3.WhenSynchronized.IsCompleted.Should().BeTrue();
        operations.Should().BeEmpty();
    }

    private static MeterListener StartStaleValueListener(ConcurrentQueue<string> operations)
    {
        var staleValueCount = FusionInstruments.RemoteComputedCacheStaleValueCount;
        staleValueCount.Name.Should().Be("remote_computed.cache.stale_value.count");
        staleValueCount.Unit.Should().Be("{request}");
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) => {
            if (ReferenceEquals(instrument, staleValueCount))
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) => {
            value.Should().Be(1);
            tags.Length.Should().Be(1);
            operations.Enqueue(GetTag(tags, "operation"));
        });
        listener.Start();
        return listener;
    }

    private static string GetTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string name)
    {
        foreach (var tag in tags)
            if (tag.Key == name)
                return (string)tag.Value!;

        return "";
    }
}


// The same fallback across a real peer change. RpcTestConnection binds to one server peer for
// life, so this drives the client through two connections sharing a client peer ref but not a
// server peer ref - the second handshake then reports Changed, which resends the pending call
// instead of reconnecting it. Nothing invalidates the served value up front: the resent call
// still carries its hash, so the new peer confirms or displaces it exactly as the old one would.
[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class FusionRpcServeStalePeerChangeTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var fusion = services.AddFusion();
        fusion.AddServerAndClient<IServeStaleTester, ServeStaleTester>();
        services.AddSingleton<IRemoteComputedCache>(
            c => new InMemoryRemoteComputedCache(InMemoryRemoteComputedCache.Options.Default, c));

        // AddTestClient aliases RpcClient -> RpcTestClient, so replacing the
        // RpcTestClient registration is enough to redirect the whole chain.
        services.RemoveAll(d => d.ServiceType == typeof(RpcTestClient));
        services.AddSingleton(c => new SwitchableRpcTestClient(c));
        services.AddAlias<RpcTestClient, SwitchableRpcTestClient>();
    }

    protected override void StartServices(IServiceProvider services)
    {
        // The test builds and switches its own connections.
    }

    [Fact]
    public async Task NewPeerConfirmsStaleValueTest()
    {
        await using var services = CreateServices();
        var (connection1, connection2) = CreateConnections(services);
        var client = services.RpcHub().GetClient<IServeStaleTester>();

        var c2 = await ServeStale(client, connection1);

        // The new peer produces the same value, so the resent call gets "match"
        await SwitchTo(services, connection2);
        await c2.WhenSynchronized.WaitAsync(Timeout);
        c2.IsConsistent().Should().BeTrue("a confirmed stale value must stay in place");
        c2.Value.Should().Be("v-1");
        connection1.ClientPeer.ConnectionState.Value.Handshake!.RemotePeerId
            .Should().Be(connection2.ServerPeer.Id); // The peer really did change
    }

    [Fact]
    public async Task NewPeerDisplacesStaleValueTest()
    {
        await using var services = CreateServices();
        var (connection1, connection2) = CreateConnections(services);
        var client = services.RpcHub().GetClient<IServeStaleTester>();
        var server = services.GetRequiredService<ServeStaleTester>();

        var c2 = await ServeStale(client, connection1);
        server.Set("1", "b"); // Changes while the client is offline

        // The new peer produces a different value, so the resent call displaces the served one
        await SwitchTo(services, connection2);
        await c2.WhenSynchronized.WaitAsync(Timeout);
        c2.IsConsistent().Should().BeFalse("a displaced stale value must be invalidated");
        var c3 = (RemoteComputed<string>)await c2.Update();
        c3.Value.Should().Be("b");
        c3.WhenSynchronized.IsCompleted.Should().BeTrue();
    }

    // Private methods

    private static (RpcTestConnection Connection1, RpcTestConnection Connection2) CreateConnections(
        IServiceProvider services)
    {
        var testClient = services.GetRequiredService<SwitchableRpcTestClient>();
        var clientPeerRef = RpcRef.Default;
        var connection1 = new RpcTestConnection(testClient, clientPeerRef, RpcRef.NewServer("server-1"));
        var connection2 = new RpcTestConnection(testClient, clientPeerRef, RpcRef.NewServer("server-2"));
        connection1.ServerPeer.Id.Should().NotBe(connection2.ServerPeer.Id);
        return (connection1, connection2);
    }

    // Returns a served stale computed whose validating call is still pending
    private async Task<RemoteComputed<string>> ServeStale(
        IServeStaleTester client, RpcTestConnection connection1)
    {
        var testClient = connection1.TestClient as SwitchableRpcTestClient;
        testClient!.Connection = connection1;
        await connection1.Connect();

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.Get("1"));
        c1.Value.Should().Be("v-1");
        await c1.WhenSynchronized.WaitAsync(Timeout);

        await connection1.Disconnect();
        c1.Invalidate();
        var c2 = (RemoteComputed<string>)await c1.Update(); // Disconnected + cached value -> serve-stale
        c2.Value.Should().Be("v-1");
        c2.WhenSynchronized.IsCompleted.Should().BeFalse();
        return c2;
    }

    private static async Task SwitchTo(IServiceProvider services, RpcTestConnection connection2)
    {
        services.GetRequiredService<SwitchableRpcTestClient>().Connection = connection2;
        await connection2.Connect();
    }
}
