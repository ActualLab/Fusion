using ActualLab.Rpc;
using ActualLab.Rpc.Testing;

namespace ActualLab.Tests.Rpc;

public class RpcFirstHandshakeTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var rpc = services.AddRpc();
        rpc.AddServerAndClient<ITestRemoteExecService, TestRemoteExecService>();
    }

    protected override void StartServices(IServiceProvider services)
    {
        // The base implementation connects right away; here the connection must
        // stay down until the test issues its call, so that call is queued
        // before the very first handshake rather than after one.
        var testClient = services.GetRequiredService<RpcTestClient>();
        testClient.CreateDefaultConnection(isBackend: false);
        testClient.CreateDefaultConnection(isBackend: true);
    }

    [Fact]
    public async Task AwaitOnlyCallSurvivesVeryFirstHandshake()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<ITestRemoteExecService>();

        // AwaitForConnection without AllowReconnect. RpcPeerChangeKind.ChangedToVeryFirst
        // is the only thing keeping OutboundCalls.Reconnect (RpcPeer.cs:421) from aborting
        // this call the moment the handshake it's waiting for succeeds.
        var task = client.AwaitOnlyDelay(TimeSpan.FromMilliseconds(10));
        await Delay(0.05);
        task.IsCompleted.Should().BeFalse();

        await connection.Connect();
        var result = await task;
        result.Should().Be(TimeSpan.FromMilliseconds(10));
        await AssertNoCalls(connection.ClientPeer, Out);
    }
}
