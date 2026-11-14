using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;

namespace ActualLab.Tests.Rpc;

public class RpcInboundCallPeerChangeTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var commander = services.AddCommander();
        commander.AddService<TestRpcService>();

        var rpc = services.AddRpc();
        rpc.AddServerAndClient<ITestRpcService, TestRpcService>();

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
    public async Task PeerChangeShouldCancelOldInboundCallsOnly()
    {
        // arrange: two client peers that connect to the same server peer one after another
        await using var services = CreateServices();
        var testClient = services.GetRequiredService<SwitchableRpcTestClient>();
        var client = services.RpcHub().GetClient<ITestRpcService>();
        var serverPeerRef = RpcRef.NewServer("server");
        var connection1 = new RpcTestConnection(testClient, RpcRef.Default, serverPeerRef);
        var connection2 = new RpcTestConnection(testClient, RpcRef.NewClient("client-2"), serverPeerRef);
        var serverPeer = connection1.ServerPeer;
        connection2.ServerPeer.Should().BeSameAs(serverPeer);

        testClient.Connection = connection1;
        await connection1.Connect().WaitAsync(TimeSpan.FromSeconds(10));
        _ = client.Delay(TimeSpan.FromSeconds(30));
        await TestExt.When(
            () => serverPeer.InboundCalls.Count.Should().Be(1),
            TimeSpan.FromSeconds(5));
        var oldCall = serverPeer.InboundCalls.Single();

        // act: the server peer gets a handshake from another remote peer
        // The first client peer is stopped, otherwise it would compete for the second connection.
        // The second one must not exist before this point for the same reason.
        await connection1.Disconnect().WaitAsync(TimeSpan.FromSeconds(10));
        await connection1.ClientPeer.Stop().WaitAsync(TimeSpan.FromSeconds(10));
        testClient.Connection = connection2;
        await connection2.Connect().WaitAsync(TimeSpan.FromSeconds(10));
        Task<TimeSpan> newCallTask;
        using (new RpcOutboundCallSetup(connection2.ClientPeer).Activate())
            newCallTask = client.Delay(TimeSpan.FromSeconds(1));
        await TestExt.When(
            () => serverPeer.InboundCalls.Count.Should().Be(1),
            TimeSpan.FromSeconds(5));
        var newCall = serverPeer.InboundCalls.Single();

        // assert
        connection2.ClientPeer.Id.Should().NotBe(connection1.ClientPeer.Id);
        newCall.Should().NotBeSameAs(oldCall);
        oldCall.CallCancelToken.IsCancellationRequested.Should().BeTrue();
        newCall.CallCancelToken.IsCancellationRequested.Should().BeFalse();
        (await newCallTask.WaitAsync(TimeSpan.FromSeconds(10))).Should().Be(TimeSpan.FromSeconds(1));
    }
}
