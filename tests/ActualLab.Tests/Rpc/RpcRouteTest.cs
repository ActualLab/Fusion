using ActualLab.Rpc;

namespace ActualLab.Tests.Rpc;

// GetConnectionKind is the one derivation of a route's connection kind: RpcPeer's constructor uses
// it, and so does the ownership test an inbound invalidation runs - which must not mint a peer.
public class RpcRouteTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void ARouteCarriesNoConnectionKindOfItsOwnByDefault()
        => RpcRoute.NewStatic(RpcRef.Local).ConnectionKind.Should().Be(RpcPeerConnectionKind.None);

    [Fact]
    public void GetConnectionKindFallsBackToTheDetector()
    {
        // The default detector reads it off the ref, which is where a local ref declares it
        RpcRoute.NewStatic(RpcRef.Local).GetConnectionKind(RpcPeerOptions.Default)
            .Should().Be(RpcPeerConnectionKind.Local);
        RpcRoute.NewStatic(RpcRef.Loopback).GetConnectionKind(RpcPeerOptions.Default)
            .Should().Be(RpcPeerConnectionKind.Loopback);
    }

    [Fact]
    public void AnExplicitConnectionKindWinsOverTheDetector()
    {
        var options = RpcPeerOptions.Default with {
            ConnectionKindDetector = _ => RpcPeerConnectionKind.Remote,
        };
        var route = new RpcRoute(RpcRef.Local) { ConnectionKind = RpcPeerConnectionKind.Loopback };

        route.GetConnectionKind(options).Should().Be(RpcPeerConnectionKind.Loopback);
    }

    [Fact]
    public void GetConnectionKindCanStillReturnNone()
    {
        // RpcPeer maps None to Remote itself, and a test for Local doesn't care either way
        var options = RpcPeerOptions.Default with {
            ConnectionKindDetector = _ => RpcPeerConnectionKind.None,
        };

        RpcRoute.NewStatic(RpcRef.Local).GetConnectionKind(options)
            .Should().Be(RpcPeerConnectionKind.None);
    }
}
