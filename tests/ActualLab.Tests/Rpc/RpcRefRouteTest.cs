using ActualLab.Rpc;

namespace ActualLab.Tests.Rpc;

public class RpcRefRouteTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddSingleton<RpcPeerOptions>(_ => RpcPeerOptions.Default with {
            PeerRemoveDelayProvider = _ => TimeSpan.Zero,
        });
    }

    [Fact]
    public void RoutelessRefTest()
    {
        var rpcRef = new RpcRef { HostInfo = "routeless" }.Initialize();
        var route = rpcRef.Route;
        route.IsStatic.Should().BeTrue();
        route.IsChanged.Should().BeFalse();
        route.Ref.Should().BeSameAs(rpcRef);
        rpcRef.Route.Should().BeSameAs(route);
        rpcRef.Reset().Should().BeSameAs(route);
    }

    [Fact]
    public void RouteRemintTest()
    {
        var rpcRef = new TestRoutedRef();
        var route1 = rpcRef.Route;
        route1.IsStatic.Should().BeFalse();
        route1.Version.Should().Be(1);
        rpcRef.CreateRouteCount.Should().Be(1);
        rpcRef.Route.Should().BeSameAs(route1);

        // A burst of changes with no interleaved Route reads must coalesce into a single re-mint
        for (var i = 0; i < 5; i++)
            route1.MarkChanged();

        var route2 = rpcRef.Route;
        route2.Should().NotBeSameAs(route1);
        route2.Version.Should().Be(2);
        rpcRef.CreateRouteCount.Should().Be(2);
        rpcRef.Route.Should().BeSameAs(route2);
        rpcRef.CreateRouteCount.Should().Be(2);
    }

    [Fact]
    public void ResetTest()
    {
        var rpcRef = new TestRoutedRef();
        var route1 = rpcRef.Route;
        var route2 = rpcRef.Reset();
        route1.IsChanged.Should().BeTrue();
        route1.WhenChanged.IsCompleted.Should().BeTrue();
        route2.Should().NotBeSameAs(route1);
        rpcRef.Route.Should().BeSameAs(route2);
        rpcRef.ToString().Should().Be(rpcRef.Address); // ToString must stay stable across resets
    }

    [Fact]
    public void RouteToStringTest()
    {
        var routelessRef = new RpcRef { HostInfo = "routeless-to-string" }.Initialize();
        routelessRef.Route.ToString().Should().Be(routelessRef.ToString());

        var rpcRef = new TestRoutedRef();
        var route = rpcRef.Route;
        var s = route.ToString();
        s.Should().Be($"{rpcRef.Address} [v1]");
        route.ToString().Should().BeSameAs(s); // Must be cached

        route.MarkChanged();
        var sChanged = route.ToString();
        sChanged.Should().Be($"{rpcRef.Address} [v1x]");
        route.ToString().Should().BeSameAs(sChanged); // Must be cached

        rpcRef.Route.ToString().Should().Be($"{rpcRef.Address} [v2]");
    }

    [Fact]
    public async Task GetPeerReplacementTest()
    {
        await using var services = CreateServices();
        var hub = services.RpcHub();
        var rpcRef = new TestRoutedRef();

        var peer1 = hub.GetPeer(rpcRef);
        peer1.Route.Should().BeSameAs(rpcRef.Route);
        peer1.ConnectionKind.Should().Be(RpcPeerConnectionKind.Local);
        hub.GetPeer(rpcRef).Should().BeSameAs(peer1);

        var oldRoute = rpcRef.Route!;
        rpcRef.Reset();
        var peer2 = hub.GetPeer(rpcRef);
        peer2.Should().NotBeSameAs(peer1);
        peer2.Route.Should().BeSameAs(rpcRef.Route);
        peer1.Route.Should().BeSameAs(oldRoute); // The draining peer keeps observing its own generation

        // The old peer is disposed on route change; its removal must not evict the replacement
        await TestExt.When(
            () => peer1.WhenDisposed.Should().NotBeNull(),
            TimeSpan.FromSeconds(5));
        await peer1.WhenDisposed!;
        await Delay(0.2); // Wait for RemovePeer with zero PeerRemoveDelay
        hub.InternalServices.Peers[peer2.Route].Should().BeSameAs(peer2);
        hub.InternalServices.Peers.ContainsKey(oldRoute).Should().BeFalse();
        hub.GetPeer(rpcRef).Should().BeSameAs(peer2);
    }

    [Fact]
    public async Task GetPeerRaceTest()
    {
        await using var services = CreateServices();
        var hub = services.RpcHub();
        var rpcRef = new TestRoutedRef();

        var resetTask = Task.Run(() => {
            for (var i = 0; i < 300; i++) {
                rpcRef.Reset();
                if (i % 10 == 0)
                    Thread.Yield();
            }
        });
        var getTasks = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => {
                while (!resetTask.IsCompleted) {
                    var peer = hub.GetPeer(rpcRef);
                    peer.Route.IsStatic.Should().BeFalse();
                }
            }))
            .ToArray();
        await Task.WhenAll(getTasks.Concat([resetTask]));

        var finalPeer = hub.GetPeer(rpcRef);
        finalPeer.Route.Should().BeSameAs(rpcRef.Route);
        finalPeer.Route.IsChanged.Should().BeFalse();
    }

    // Nested types

    private sealed class TestRoutedRef : RpcRef
    {
        private int _createRouteCount;

        public int CreateRouteCount => _createRouteCount;

        public TestRoutedRef()
        {
            HostInfo = "test-routed-ref";
            UseReferentialEquality = true;
            Initialize();
        }

        protected override RpcRoute CreateRoute()
        {
            Interlocked.Increment(ref _createRouteCount);
            return new RpcRoute(this) { ConnectionKind = RpcPeerConnectionKind.Local };
        }
    }
}
