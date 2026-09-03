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
        // Reconnect attempts come well within ReconnectTimeout (1s), unless parked
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

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetWithReconnectTimeout("1"));
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        c1.Invalidate();
        _inboundCallDelayMs = 1000;
        var updateTask = c1.Update();
        await Delay(0.3);
        await connection.Disconnect();
        await Delay(0.3);
        await connection.Connect(); // Within ReconnectTimeout (1s)
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

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetWithReconnectTimeout("1"));
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        c1.Invalidate();
        _inboundCallDelayMs = 1000;
        var sw = Stopwatch.StartNew();
        var updateTask = c1.Update();
        await Delay(0.3);
        await connection.Disconnect();
        var c2 = (RemoteComputed<string>)await updateTask;
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(1.2), "ReconnectTimeout must run out first");
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

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetWithReconnectTimeout("1"));
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        await connection.Disconnect();
        server.Set("1", "b"); // Changes while the client is offline
        c1.Invalidate();
        var sw = Stopwatch.StartNew();
        var c2 = (RemoteComputed<string>)await c1.Update();
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(0.9), "ReconnectTimeout must run out first");
        c2.Value.Should().Be("v-1");
        c2.WhenSynchronized.IsCompleted.Should().BeFalse();
        operations.Should().BeEquivalentTo("connection_check");

        await connection.Connect();
        await c2.WhenSynchronized.WaitAsync(Timeout);
        c2.IsInvalidated().Should().BeTrue("the fresh value must displace the stale one");
        var c3 = (RemoteComputed<string>)Computed.GetExisting(() => client.GetWithReconnectTimeout("1"))!;
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

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetWithReconnectTimeout("1"));
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        await connection.Disconnect();
        server.Set("1", "b");
        c1.Invalidate();
        var updateTask = c1.Update();
        await Delay(0.3);
        await connection.Connect(); // Within ReconnectTimeout (1s)
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

        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetWithReconnectTimeout("1"));
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        _reconnectDelayer!.ParkDelay = TimeSpan.FromMinutes(1); // As if the OS reported "offline"
        await connection.Disconnect();
        c1.Invalidate();
        var sw = Stopwatch.StartNew();
        var c2 = (RemoteComputed<string>)await c1.Update();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(0.5), "a reconnect parked past ReconnectTimeout is not waited for");
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
    public async Task ColdMissWithoutReconnectTimeoutKeepsWaitingTest()
    {
        var operations = new ConcurrentQueue<string>();
        using var listener = StartStaleValueListener(operations);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IServeStaleTester>();

        await connection.Disconnect();
        var getTask = client.GetNoCache("1"); // Nothing to fall back to, ReconnectTimeout = 0
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
    public async Task ColdMissFailsAfterReconnectTimeoutTest()
    {
        var operations = new ConcurrentQueue<string>();
        using var listener = StartStaleValueListener(operations);
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IServeStaleTester>();

        // Nothing to fall back to: the call fails like any other RPC call would
        await connection.Disconnect();
        var sw = Stopwatch.StartNew();
        var timeout = await Assert.ThrowsAsync<RpcTimeoutException>(() => client.GetNoCacheWithReconnectTimeout("1"));
        timeout.TimeoutKind.Should().Be(RpcTimeoutKind.Reconnect);
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(0.9), "ReconnectTimeout must run out first");
        await connection.Connect();
        var c1 = (RemoteComputed<string>)await Computed.Capture(() => client.GetNoCacheWithReconnectTimeout("1"));
        c1.Value.Should().Be("v-1");

        // Same for a mid-call disconnect: the in-flight call fails, and the computed carries its error
        c1.Invalidate();
        _inboundCallDelayMs = 1000;
        sw.Restart();
        var updateTask = c1.Update().AsTask();
        await Delay(0.3);
        await connection.Disconnect();
        var c2 = (RemoteComputed<string>)await updateTask.WaitAsync(Timeout);
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(1.2), "ReconnectTimeout must run out first");
        c2.Error.Should().BeOfType<RpcTimeoutException>().Which.TimeoutKind.Should().Be(RpcTimeoutKind.Reconnect);

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
