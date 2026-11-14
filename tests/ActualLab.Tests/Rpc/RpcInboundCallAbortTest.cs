using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;

namespace ActualLab.Tests.Rpc;

public class RpcInboundCallAbortTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var commander = services.AddCommander();
        commander.AddService<TestRpcService>();

        var rpc = services.AddRpc();
        rpc.AddServerAndClient<ITestRpcService, TestRpcService>();
    }

    [Fact]
    public async Task PeerStopShouldCancelRunningInboundCalls()
    {
        // arrange
        await using var services = CreateServices();
        var serverPeer = GetServerPeer(services);
        var client = services.RpcHub().GetClient<ITestRpcService>();
        _ = client.Delay(TimeSpan.FromSeconds(30));
        await TestExt.When(
            () => serverPeer.InboundCalls.Count.Should().Be(1),
            TimeSpan.FromSeconds(5));
        var call = serverPeer.InboundCalls.Single();
        call.CallCancelToken.IsCancellationRequested.Should().BeFalse();

        // act
        var stopTask = serverPeer.Stop();

        // assert
        call.CallCancelToken.IsCancellationRequested.Should().BeTrue(
            "stopping the peer must cancel its inbound calls right away, on the stopping thread");
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
        serverPeer.InboundCalls.Count.Should().Be(0);
    }

    [Fact]
    public async Task CallRegisteredAfterPeerChangeShouldBeCancelled()
    {
        // arrange
        await using var services = CreateServices();
        var serverPeer = GetServerPeer(services);
        var methodDef = serverPeer.Hub.ServiceRegistry[typeof(ITestRpcService)][
            typeof(ITestRpcService).GetMethod(nameof(ITestRpcService.Delay))!];
        using var peerChangedCts = new CancellationTokenSource();
        peerChangedCts.Cancel();
        var message = new RpcInboundMessage(methodDef.CallType.Id, 1_000_000, methodDef.Ref, default, null) {
            Arguments = ArgumentList.New(TimeSpan.FromSeconds(30), CancellationToken.None),
        };
        var call = new RpcInboundContext(serverPeer, message, peerChangedCts.Token).Call;

        // act
        var processTask = call.Process(CancellationToken.None);

        // assert
        call.CallCancelToken.IsCancellationRequested.Should().BeTrue();
        await processTask.WaitAsync(TimeSpan.FromSeconds(5));
        serverPeer.InboundCalls.Get(call.Id).Should().BeNull();
    }

    // Private methods

    private static RpcPeer GetServerPeer(IServiceProvider services)
        => services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ServerPeer;
}
