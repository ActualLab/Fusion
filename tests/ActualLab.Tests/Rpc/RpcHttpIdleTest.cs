#if NET5_0_OR_GREATER
using ActualLab.Rpc;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

// An RPC connection holds its request body open for its whole lifetime and writes almost nothing
// into it while the peer is idle, which is exactly what Kestrel's MinRequestBodyDataRate
// (240 B/s past a 5s grace period by default) is built to kill. The abort surfaces on HTTP/2 as a
// connection-level fault, so it also takes down every other RPC stream sharing that HTTP/2
// connection - a reverse proxy multiplexes many clients onto a few backend connections.
[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcHttpIdleTest : RpcTestBase
{
    private static readonly TimeSpan IdleDuration = TimeSpan.FromSeconds(12);

    public RpcHttpIdleTest(ITestOutputHelper @out) : base(@out)
        => UseHttpClient = true;

    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);
        var rpc = services.AddRpc();
        var commander = services.AddCommander();
        if (isClient) {
            rpc.AddClient<ITestRpcService>();
            commander.AddService<ITestRpcService>();
        }
        else {
            rpc.AddServer<ITestRpcService, TestRpcService>();
            commander.AddService<TestRpcService>();
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task IdleConnectionSurvivesTest()
    {
        await ResetClientServices();
        await using var _ = await WebHost.Serve();
        var hub = ClientServices.RpcHub();
        var client = hub.GetClient<ITestRpcService>();
        var peer = hub.GetClientPeer(ClientPeerRef);

        (await client.Div(6, 2)).Should().Be(3);
        var connectedState = peer.ConnectionState.Value;
        connectedState.IsConnected().Should().BeTrue();

        await Delay(IdleDuration.TotalSeconds);

        connectedState.WhenDisconnected.IsCompleted.Should().BeFalse();
        peer.ConnectionState.Value.Should().BeSameAs(connectedState);
        (await client.Div(6, 2)).Should().Be(3);
    }
}
#endif
