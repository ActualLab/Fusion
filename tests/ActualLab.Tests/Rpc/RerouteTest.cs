using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RerouteTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    public sealed class ReroutePeerRef : RpcPeerRef
    {
        public ReroutePeerRef(string address) {
            Address = address;
            RouteState = new ();
            Initialize();
        }

        public ReroutePeerRef UpdateRouteState() {
            RouteState = new ();
            return this;
        }
    }

    [Fact]
    public async Task RerouteImmediatelyWhenNodeIsDeadTest()
    {
        var client1PeerRef = new ReroutePeerRef("client1");
        var server1PeerRef = RpcPeerRef.NewServer("server1");
        var client2PeerRef = new ReroutePeerRef("client2");
        var server2PeerRef = RpcPeerRef.NewServer("server2");
        await using var services = CreateServices(s => {
            s.AddRpc().AddServerAndClient<ITestRpcService, TestRpcService>();
            s.AddSingleton<RpcOutboundCallOptions>(_ => RpcOutboundCallOptions.Default with {
                RouterFactory = _ => _ => client1PeerRef.RouteState!.IsChanged()
                    ? client2PeerRef
                    : client1PeerRef,
            });
            // Make Maintain loop very slow to ensure it's NOT the one doing the rerouting
            s.AddSingleton(_ => RpcLimits.Default with {
                CallTimeoutCheckPeriod = TimeSpan.FromSeconds(10).ToRandom(0),
            });
        });
        var testClient = services.GetRequiredService<RpcTestClient>();
        var connection1 = testClient.CreateConnection(client1PeerRef, server1PeerRef);
        await connection1.Connect();
        var connection2 = testClient.CreateConnection(client2PeerRef, server2PeerRef);
        await connection2.Connect();

        var clientPeer = connection1.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        // 1. Warm up
        (await client.Div(6, 2)).Should().Be(3);

        // 2. Disconnect and ensure it stays disconnected
        await connection1.Disconnect();
        clientPeer.IsConnected().Should().BeFalse();

        // 3. Make a call - it should be delayed because it's disconnected
        var callTask = client.Div(10, 2);
        await Task.Delay(100);
        callTask.IsCompleted.Should().BeFalse();

        // 4. Mark RouteState as changed
        var startTime = CpuTimestamp.Now;
        client1PeerRef.RouteState!.MarkChanged();

        // 5. The call should be rerouted and succeed IMMEDIATELY
        // RpcHub.GetPeer should detect the change and throw RpcRerouteException,
        // or RpcPeer should detect it.
        (await callTask).Should().Be(5);
        startTime.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RerouteCycleTest()
    {
        var clientPeerRef = new ReroutePeerRef("client");
        var serverPeerRef = RpcPeerRef.NewServer("server");
        await using var services = CreateServices(s => {
            s.AddRpc().AddServerAndClient<ITestRpcService, TestRpcService>();
            s.AddSingleton<RpcOutboundCallOptions>(_ => RpcOutboundCallOptions.Default with {
                RouterFactory = _ => _ => clientPeerRef.UpdateRouteState(),
            });
        });
        var testClient = services.GetRequiredService<RpcTestClient>();
        var connection = testClient.CreateConnection(clientPeerRef, serverPeerRef);
        await connection.Connect();

        var client = services.RpcHub().GetClient<ITestRpcService>();

        // 1. Warm up
        (await client.Div(6, 2)).Should().Be(3);

        // 2. Mark RouteState as changed
        clientPeerRef.RouteState!.MarkChanged();

        // 3. Make a call
        var callTask = client.Div(10, 2);
        var result = await callTask;
        result.Should().Be(5);
    }
}
