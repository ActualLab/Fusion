using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcReconnectTimeoutTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private ParkingReconnectDelayer? _reconnectDelayer;
    private bool _mustConnectOnStart = true;

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

    protected override void StartServices(IServiceProvider services)
    {
        if (_mustConnectOnStart) {
            base.StartServices(services);
            return;
        }

        // The connections exist, but nothing connects them: the peers have never been connected
        var testClient = services.GetRequiredService<RpcTestClient>();
        testClient.CreateDefaultConnection(isBackend: false);
        testClient.CreateDefaultConnection(isBackend: true);
    }

    [Fact]
    public async Task ReconnectTimeoutResolutionTest()
    {
        await using var services = CreateServices();
        var serviceDef = services.RpcHub().ServiceRegistry[typeof(ITestRpcService)];
        serviceDef["RenamedMethod"].OutboundCallTimeouts.ReconnectTimeout.Should().Be(TimeSpan.FromSeconds(2.5));
        serviceDef["DelayWithReconnectTimeout:2"].OutboundCallTimeouts.ReconnectTimeout.Should().Be(TimeSpan.FromSeconds(1));
        serviceDef["Div:2"].OutboundCallTimeouts.ReconnectTimeout.Should().Be(TimeSpan.Zero, "the default is 0");

        RpcCallTimeouts.None.ReconnectTimeout.Should().Be(TimeSpan.Zero);
        var timeouts = new RpcCallTimeouts { ReconnectTimeout = TimeSpan.FromSeconds(-1) };
        timeouts.ReconnectTimeout.Should().Be(TimeSpan.Zero, "negative timeouts are clamped");
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
        await AssertTimeout(RpcTimeoutKind.Connect,
            () => clientPeer.WhenConnectedOrReroute(TimeSpan.FromSeconds(0.5)));
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(0.4));
        await connection.Connect();

        // A reconnect parked past the deadline fails the wait at once
        _reconnectDelayer!.ParkDelay = TimeSpan.FromMinutes(1);
        await connection.Disconnect();
        sw.Restart();
        await AssertTimeout(RpcTimeoutKind.Connect,
            () => clientPeer.WhenConnectedOrReroute(TimeSpan.FromSeconds(5)));
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
    public async Task MidCallDisconnectFailsAfterReconnectTimeoutTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        await clientPeer.WhenConnected(Timeout);

        var sw = Stopwatch.StartNew();
        var callTask = client.DelayWithReconnectTimeout(TimeSpan.FromSeconds(3));
        await Delay(0.3);
        await connection.Disconnect();
        await AssertTimeout(RpcTimeoutKind.Reconnect,() => callTask);
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(1.2), "ReconnectTimeout must run out first");
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2.5));
        clientPeer.OutboundCalls.Count.Should().Be(0, "the failed call must be unregistered");

        // A call without ReconnectTimeout survives the same disconnect and completes on reconnect
        await connection.Connect();
        callTask = client.Delay(TimeSpan.FromSeconds(1));
        await Delay(0.3);
        await connection.Disconnect();
        await Delay(1.5);
        callTask.IsCompleted.Should().BeFalse();
        await connection.Connect();
        (await callTask.WaitAsync(Timeout)).Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task MidCallDisconnectReconnectWithinTimeoutCompletesTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        await clientPeer.WhenConnected(Timeout);

        var callTask = client.DelayWithReconnectTimeout(TimeSpan.FromSeconds(1));
        await Delay(0.3);
        await connection.Disconnect();
        await Delay(0.3);
        await connection.Connect(); // Within ReconnectTimeout (1s)
        (await callTask.WaitAsync(Timeout)).Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DisconnectedAtCallTimeFailsAfterReconnectTimeoutTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        await clientPeer.WhenConnected(Timeout);

        await connection.Disconnect();
        var sw = Stopwatch.StartNew();
        await AssertTimeout(RpcTimeoutKind.Reconnect,
            () => client.DelayWithReconnectTimeout(TimeSpan.FromMilliseconds(100)));
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(0.9), "ReconnectTimeout must run out first");
        clientPeer.OutboundCalls.Count.Should().Be(0, "the failed call must be unregistered");

        // A call without ReconnectTimeout keeps waiting: the query ConnectTimeout is infinite
        var callTask = client.Delay(TimeSpan.FromMilliseconds(100));
        await Delay(1.5);
        callTask.IsCompleted.Should().BeFalse();
        await connection.Connect();
        (await callTask.WaitAsync(Timeout)).Should().Be(TimeSpan.FromMilliseconds(100));
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
        var callTask = client.DelayWithReconnectTimeout(TimeSpan.FromSeconds(3));
        await Delay(0.3);
        await connection.Disconnect();
        var sw = Stopwatch.StartNew();
        await AssertTimeout(RpcTimeoutKind.Reconnect,() => callTask);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(0.5), "a reconnect parked past the deadline fails the call at once");

        sw.Restart();
        await AssertTimeout(RpcTimeoutKind.Reconnect,
            () => client.DelayWithReconnectTimeout(TimeSpan.FromMilliseconds(100)));
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(0.5));

        _reconnectDelayer.ParkDelay = null;
        _reconnectDelayer.CancelDelays();
        await connection.Connect();
        (await client.DelayWithReconnectTimeout(TimeSpan.FromMilliseconds(100)).WaitAsync(Timeout))
            .Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task NeverConnectedPeerWaitsForConnectTimeoutTest()
    {
        _mustConnectOnStart = false;
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        clientPeer.HasEverConnected.Should().BeFalse();

        // Not a reconnect, so ReconnectTimeout doesn't apply: the (infinite) query ConnectTimeout does
        var callTask = client.DelayWithReconnectTimeout(TimeSpan.FromMilliseconds(100));
        await Delay(1.5);
        callTask.IsCompleted.Should().BeFalse();
        await connection.Connect();
        (await callTask.WaitAsync(Timeout)).Should().Be(TimeSpan.FromMilliseconds(100));
        clientPeer.HasEverConnected.Should().BeTrue();
    }

    // Private methods

    private static async Task AssertTimeout(RpcTimeoutKind timeoutKind, Func<Task> action)
    {
        var timeout = await Assert.ThrowsAsync<RpcTimeoutException>(action);
        timeout.TimeoutKind.Should().Be(timeoutKind);
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
