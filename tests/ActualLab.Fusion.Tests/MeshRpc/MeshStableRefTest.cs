using ActualLab.Fusion.Testing;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.MeshRpc;

public class MeshStableRefTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    [Fact]
    public async Task StableRefAcrossSwapsTest()
    {
        await using var testHosts = NewTestHosts();
        var host0 = testHosts.NewHost();
        var host1 = testHosts.NewHost();
        await Task.WhenAll(host0.WhenStarted, host1.WhenStarted);

        var meshMap = testHosts.MeshMap;
        var rpcRef = meshMap.GetShardRef(0);
        var route1 = (RpcShardRoute)rpcRef.Route;
        route1.Host.Should().Be(host0);

        meshMap.Swap(0, 1);
        await route1.WhenChanged.WaitAsync(TimeSpan.FromSeconds(5));

        meshMap.GetShardRef(0).Should().BeSameAs(rpcRef);
        var route2 = (RpcShardRoute)rpcRef.Route;
        route2.Should().NotBeSameAs(route1);
        route2.Version.Should().Be(route1.Version + 1);
        route2.Host.Should().Be(host1);
        rpcRef.ToString().Should().Be(rpcRef.Address); // Stable across route generations
    }

    [Fact]
    public async Task LocalRemoteFlipTest()
    {
        await using var testHosts = NewTestHosts();
        var host0 = testHosts.NewHost();
        var host1 = testHosts.NewHost();
        await Task.WhenAll(host0.WhenStarted, host1.WhenStarted);

        // host0 calls shard 0, which initially maps to host0 itself -> local execution
        var commander = host0.Commander();
        var service = host0.GetRequiredService<IRpcRerouteTestService>();
        var hub = host0.Services.RpcHub();
        var rpcRef = testHosts.MeshMap.GetShardRef(0);

        await commander.Call(new RpcRerouteTestService_SetValue(0, "k", "v0", 0));
        var computed = await Computed.Capture(() => service.GetValue(0, "k"));
        computed.Value.HostId.Should().Be(host0.Id);
        computed.Value.Value.Should().Be("v0");

        var peer1 = hub.GetPeer(rpcRef);
        peer1.ConnectionKind.Should().Be(RpcPeerConnectionKind.Local);

        // Flip: shard 0 now maps to host1 -> remote from host0's perspective
        testHosts.MeshMap.Swap(0, 1);
        await ComputedTest.When(async ct => {
            var v = await computed.Use(ct);
            v.HostId.Should().Be(host1.Id);
            v.Value.Should().Be("");
        }, TimeSpan.FromSeconds(5));

        var peer2 = hub.GetPeer(rpcRef);
        peer2.Should().NotBeSameAs(peer1);
        peer2.ConnectionKind.Should().Be(RpcPeerConnectionKind.Remote);

        // Flip back: local execution again, the original value must survive
        testHosts.MeshMap.Swap(0, 1);
        await ComputedTest.When(async ct => {
            var v = await computed.Use(ct);
            v.HostId.Should().Be(host0.Id);
            v.Value.Should().Be("v0");
        }, TimeSpan.FromSeconds(5));

        var peer3 = hub.GetPeer(rpcRef);
        peer3.Should().NotBeSameAs(peer2);
        peer3.ConnectionKind.Should().Be(RpcPeerConnectionKind.Local);
    }

    // Private methods

    private static MeshHostSet NewTestHosts()
        => new((host, services) => {
            var fusion = services.AddFusion();
            fusion.AddService<IRpcRerouteTestService, RpcRerouteTestService>(host.ServiceMode);
        });
}
