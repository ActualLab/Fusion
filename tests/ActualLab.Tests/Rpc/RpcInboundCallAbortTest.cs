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
    public async Task SamePeerReconnectShouldKeepRunningInboundCalls()
    {
        // arrange
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var serverPeer = connection.ServerPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        var callTask = client.Delay(TimeSpan.FromSeconds(1.5));
        await TestExt.When(
            () => serverPeer.InboundCalls.Count.Should().Be(1),
            TimeSpan.FromSeconds(5));
        var call = serverPeer.InboundCalls.Single();

        // act
        await connection.Reconnect();
        (await client.Add(1, 1)).Should().Be(2); // Ensures the reconnect is fully processed

        // assert
        call.CallCancelToken.IsCancellationRequested.Should().BeFalse();
        (await callTask.WaitAsync(TimeSpan.FromSeconds(10))).Should().Be(TimeSpan.FromSeconds(1.5));
    }

    [Fact(Timeout = 30_000)]
    public async Task CallCountLimitResetShouldCancelRunningInboundCalls()
    {
        // arrange
        const int callCountLimit = 2;
        await using var services = CreateServices(services => services.AddSingleton(_ => new RpcLimits(false) {
            CallCountLimit = callCountLimit,
            ObjectReleasePeriod = TimeSpan.FromSeconds(1),
        }));
        var serverPeer = GetServerPeer(services);
        var client = services.RpcHub().GetClient<ITestRpcService>();

        // act
        for (var i = 0; i < callCountLimit + 3; i++)
            _ = client.Delay(TimeSpan.FromSeconds(30));
        await TestExt.When(
            () => serverPeer.InboundCalls.Count.Should().BeGreaterThan(callCountLimit),
            TimeSpan.FromSeconds(5));
        var calls = serverPeer.InboundCalls.ToList();

        // assert
        await TestExt.When(
            () => calls.Should().AllSatisfy(x => x.CallCancelToken.IsCancellationRequested.Should().BeTrue()),
            TimeSpan.FromSeconds(10));
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
