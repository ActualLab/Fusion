using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcConnectTimeoutTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private ParkingReconnectDelayer? _reconnectDelayer;

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var commander = services.AddCommander();
        commander.AddService<TestRpcService>();
        services.AddRpc().AddServerAndClient<ITestRpcService, TestRpcService>();
        // Reconnect attempts come well within every timeout used below, unless parked
        services.AddSingleton<RpcClientPeerReconnectDelayer>(c => _reconnectDelayer = new ParkingReconnectDelayer(c) {
            Delays = RetryDelaySeq.Fixed(0.25),
        });
    }

    [Fact]
    public async Task TimeoutResolutionTest()
    {
        await using var services = CreateServices();
        var serviceDef = services.RpcHub().ServiceRegistry[typeof(ITestRpcService)];
        serviceDef["RenamedMethod"].OutboundCallTimeouts.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(2.5));
        serviceDef["DelayWithConnectTimeout:2"].OutboundCallTimeouts.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(1));
        serviceDef["Div:2"].OutboundCallTimeouts.ConnectTimeout
            .Should().Be(TimeSpanExt.Infinite, "the query default is no timeout");
        serviceDef["Div:2"].OutboundCallTimeouts.CacheFallbackDelay
            .Should().Be(TimeSpan.Zero, "the default is to serve the fallback at once");

        RpcCallTimeouts.None.ConnectTimeout.Should().Be(TimeSpanExt.Infinite);
        RpcCallTimeouts.None.CacheFallbackDelay.Should().Be(TimeSpan.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RpcCallTimeouts { CacheFallbackDelay = TimeSpan.FromSeconds(-1) });
    }

    [Fact]
    public async Task ParkedReconnectFailsConnectWaitAtOnceTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        await clientPeer.WhenConnected(Timeout);

        // A reconnect attempt that is due in time is waited for
        await connection.Disconnect();
        var sw = Stopwatch.StartNew();
        await AssertConnectTimeout(() => clientPeer.WhenConnectedOrReroute(TimeSpan.FromSeconds(0.5)));
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(0.4));
        await connection.Connect();

        // A reconnect parked past the deadline fails the wait at once
        _reconnectDelayer!.ParkDelay = TimeSpan.FromMinutes(1);
        await connection.Disconnect();
        sw.Restart();
        await AssertConnectTimeout(() => clientPeer.WhenConnectedOrReroute(TimeSpan.FromSeconds(5)));
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));

        // Un-parking (the attempt is due at once) lets a new wait proceed until the peer is back
        _reconnectDelayer.ParkDelay = null;
        _reconnectDelayer.CancelDelays();
        await clientPeer.ReconnectsAt.When(x => x == default).WaitAsync(Timeout);
        var whenConnected = clientPeer.WhenConnectedOrReroute(TimeSpan.FromSeconds(5));
        await Delay(0.3);
        whenConnected.IsCompleted.Should().BeFalse();
        await connection.Connect();
        (await whenConnected.WaitAsync(Timeout)).IsConnected().Should().BeTrue();
    }

    [Fact]
    public async Task ParkedReconnectPreservesCallerCancellationTest()
    {
        await using var services = CreateServices();
        var clientPeer = new TestClientPeer(services.RpcHub(), RpcRef.NewClient("parked-cancellation").Route);
        clientPeer.ParkReconnect();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => clientPeer.WhenConnectedOrReroute(TimeSpan.FromSeconds(5), cancellationSource.Token));
        error.CancellationToken.Should().Be(cancellationSource.Token);
    }

    [Fact]
    public async Task ParkedReconnectPreservesRerouteTest()
    {
        await using var services = CreateServices();
        var rpcRef = new RoutedRpcRef() { HostInfo = "parked-reroute" }.Initialize();
        var route = rpcRef.Route;
        var clientPeer = new TestClientPeer(services.RpcHub(), route);
        clientPeer.ParkReconnect();
        route.MarkChanged();

        await Assert.ThrowsAsync<RpcRerouteException>(
            () => clientPeer.WhenConnectedOrReroute(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task DisconnectedAtCallTimeFailsAfterConnectTimeoutTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        await clientPeer.WhenConnected(Timeout);

        // ConnectTimeout governs a reconnect exactly as it governs the first connection
        await connection.Disconnect();
        var sw = Stopwatch.StartNew();
        await AssertConnectTimeout(() => client.DelayWithConnectTimeout(TimeSpan.FromMilliseconds(100)));
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(0.9), "ConnectTimeout must run out first");
        clientPeer.OutboundCalls.Count.Should().Be(0, "the failed call must be unregistered");

        // A call with the (infinite) query ConnectTimeout keeps waiting instead
        var callTask = client.Delay(TimeSpan.FromMilliseconds(100));
        await Delay(1.5);
        callTask.IsCompleted.Should().BeFalse();
        await connection.Connect();
        (await callTask.WaitAsync(Timeout)).Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task MidCallDisconnectIsNotCappedByConnectTimeoutTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        await clientPeer.WhenConnected(Timeout);

        // ConnectTimeout caps only the wait before the call is sent: a sent call stays
        // registered across the disconnect and is resent once the peer is back
        var callTask = client.DelayWithConnectTimeout(TimeSpan.FromSeconds(1));
        await Delay(0.3);
        await connection.Disconnect();
        await Delay(1.5); // Longer than the method's ConnectTimeout (1s)
        callTask.IsCompleted.Should().BeFalse();
        await connection.Connect();
        (await callTask.WaitAsync(Timeout)).Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ParkedReconnectFailsCallAtOnceTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        await clientPeer.WhenConnected(Timeout);

        _reconnectDelayer!.ParkDelay = TimeSpan.FromMinutes(1); // As if the OS reported "offline"
        await connection.Disconnect();
        var sw = Stopwatch.StartNew();
        await AssertConnectTimeout(() => client.DelayWithConnectTimeout(TimeSpan.FromMilliseconds(100)));
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(0.5),
            "a reconnect parked past the deadline fails the call at once");

        _reconnectDelayer.ParkDelay = null;
        _reconnectDelayer.CancelDelays();
        await connection.Connect();
        (await client.DelayWithConnectTimeout(TimeSpan.FromMilliseconds(100)).WaitAsync(Timeout))
            .Should().Be(TimeSpan.FromMilliseconds(100));
    }

    // Private methods

    private static async Task AssertConnectTimeout(Func<Task> action)
    {
        var timeout = await Assert.ThrowsAsync<RpcTimeoutException>(action);
        timeout.TimeoutKind.Should().Be(RpcTimeoutKind.Connect);
    }

    // Nested types

    private sealed class RoutedRpcRef : RpcRef
    {
        protected override RpcRoute CreateRoute()
            => new(this);
    }

    private sealed class TestClientPeer(RpcHub hub, RpcRoute route) : RpcClientPeer(hub, route)
    {
        public void ParkReconnect()
            => SetReconnectsAt(ReconnectDelayer.Clock.Now + TimeSpan.FromMinutes(1));
    }
}
