using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;

namespace ActualLab.Tests.Rpc;

/// <summary>
/// A peer stops running calls once its route changes or its connection state goes final, but
/// <see cref="RpcHub"/> keeps handing it out for PeerRemoveDelay afterwards. These cover what a call
/// registered in that window gets: it must complete, and it must not reach the peer it can't run on.
/// </summary>
public class RpcFinalErrorTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var commander = services.AddCommander();
        commander.AddService<TestRpcService>();
        services.AddRpc().AddServerAndClient<ITestRpcService, TestRpcService>();
    }

    [Fact]
    public async Task CallRegisteredAfterRerouteShouldRerouteTest()
    {
        await using var services = CreateServices();
        var peer = NewPeer(services, "after-reroute");
        peer.Route.MarkChanged();
        peer.OutboundCalls.TryReroute(); // What RpcPeer.OnRouteChanged does, synchronously

        var call = NewCall(services, peer);
        var resultTask = call.Invoke();

        resultTask.IsCompleted.Should().BeTrue("the latch must complete the call, not leave it pending");
        await Assert.ThrowsAsync<RpcRerouteException>(() => resultTask.WaitAsync(Timeout));
        peer.OutboundCalls.Count.Should().Be(0, "a completed call must be unregistered");
    }

    [Fact]
    public async Task CallRegisteredAfterAbortShouldFailTest()
    {
        await using var services = CreateServices();
        var peer = NewPeer(services, "after-abort");
        var error = RpcReconnectFailedException.Unspecified();
        peer.OutboundCalls.Abort(error, assumeCancelled: true); // What RpcPeer.Reset does on a final state

        var call = NewCall(services, peer);
        var resultTask = call.Invoke();

        resultTask.IsCompleted.Should().BeTrue();
        (await Assert.ThrowsAnyAsync<Exception>(() => resultTask.WaitAsync(Timeout))).Should().BeSameAs(error);
        peer.OutboundCalls.Count.Should().Be(0);
    }

    [Fact]
    public async Task RerouteShouldSupersedeTerminalErrorTest()
    {
        await using var services = CreateServices();
        var peer = NewPeer(services, "reroute-wins");
        peer.OutboundCalls.Abort(RpcReconnectFailedException.Unspecified(), assumeCancelled: true);

        // The route can change after the peer already stopped; running on the new target beats failing
        peer.Route.MarkChanged();
        peer.OutboundCalls.TryReroute();

        var call = NewCall(services, peer);
        await Assert.ThrowsAsync<RpcRerouteException>(() => call.Invoke().WaitAsync(Timeout));
    }

    [Fact]
    public async Task LatchedCallMustNotBeSentTest()
    {
        await using var services = CreateServices();
        var peer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        await peer.WhenConnected(Timeout);

        // The transport outlives the latch - that is what makes "completed, but sent anyway" reachable
        var error = RpcReconnectFailedException.Unspecified();
        peer.OutboundCalls.Abort(error, assumeCancelled: true);

        var call = NewCall(services, peer);
        (await Assert.ThrowsAnyAsync<Exception>(() => call.Invoke().WaitAsync(Timeout))).Should().BeSameAs(error);

        // Sending it anyway would run it on a peer whose caller has already been told to reroute,
        // and the $sys.Cancel that went with the reroute names an Id the remote never saw
        call.IsSent.Should().BeFalse();
    }

    [Fact]
    public async Task LatchedCallMustNotRegisterCancellationHandlerTest()
    {
        await using var services = CreateServices();
        var peer = NewPeer(services, "no-handler");
        peer.Route.MarkChanged();
        peer.OutboundCalls.TryReroute();

        using var cts = new CancellationTokenSource();
        var call = NewCall(services, peer, cts.Token);
        await Assert.ThrowsAsync<RpcRerouteException>(() => call.Invoke().WaitAsync(Timeout));

        // The call completed inside Register, so CompleteAndUnregister already ran its (empty)
        // unregistration - installing the handler afterwards would leave one nothing ever removes
        (call.CancellationHandler == default).Should().BeTrue();
    }

    [Fact]
    public async Task CallRegisteredBeforeLatchShouldStillCompleteTest()
    {
        await using var services = CreateServices();
        var peer = NewPeer(services, "before-latch");

        // Not connected, and the query default is an infinite ConnectTimeout, so this one just waits
        var call = NewCall(services, peer);
        var resultTask = call.Invoke();
        resultTask.IsCompleted.Should().BeFalse();
        peer.OutboundCalls.Count.Should().Be(1);

        peer.Route.MarkChanged();
        peer.OutboundCalls.TryReroute();
        await Assert.ThrowsAsync<RpcRerouteException>(() => resultTask.WaitAsync(Timeout));
    }

    // Private methods

    private static RpcClientPeer NewPeer(IServiceProvider services, string hostInfo)
    {
        var rpcRef = new RoutedRpcRef { HostInfo = hostInfo }.Initialize();
        return new RpcClientPeer(services.RpcHub(), rpcRef.Route);
    }

    private static RpcOutboundCall NewCall(
        IServiceProvider services, RpcPeer peer, CancellationToken cancellationToken = default)
    {
        var method = services.RpcHub().ServiceRegistry[typeof(ITestRpcService)]["DelayWithConnectTimeout:2"];
        var arguments = ArgumentList.New(TimeSpan.FromMilliseconds(10), cancellationToken);
        return new RpcOutboundContext(peer).PrepareCall(method, arguments)!;
    }

    // Nested types

    private sealed class RoutedRpcRef : RpcRef
    {
        protected override RpcRoute CreateRoute()
            => new(this);
    }
}
