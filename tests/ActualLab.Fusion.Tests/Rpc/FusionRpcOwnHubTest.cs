using ActualLab.Fusion.Tests.Services;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;

namespace ActualLab.Fusion.Tests.Rpc;

/// <summary>
/// A compute call made through a service's own class proxy must never reach the hub it came from -
/// it would call the service on itself and deadlock on the input lock its caller already holds.
/// The check runs on the send path, so it also covers a resend that lands on our own hub.
/// </summary>
public class FusionRpcOwnHubTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddFusion().AddServerAndClient<IServeStaleTester, ServeStaleTester>();
    }

    [Fact]
    public async Task OwnHubCallMustFailInsteadOfBeingSentTest()
    {
        await using var services = CreateServices();
        var (peer, call) = await NewOwnHubCall(services);

        await Assert.ThrowsAsync<InvalidOperationException>(() => call.Invoke().WaitAsync(Timeout));
        call.IsSent.Should().BeFalse("the call must fail rather than reach its own hub");
        peer.OutboundCalls.Count.Should().Be(0);
    }

    [Fact]
    public async Task OwnHubResendMustFailTooTest()
    {
        await using var services = CreateServices();
        var (peer, call) = await NewOwnHubCall(services);

        // Maintain and Reconnect resend through ResendRegistered, which the one-shot per-handshake
        // sweep used to cover: a reconnect through a load balancer can land on our own hub even
        // when the first send didn't
        call.Register();
        call.ResendRegistered();

        await Assert.ThrowsAsync<InvalidOperationException>(() => call.ResultTask.WaitAsync(Timeout));
        call.IsSent.Should().BeFalse();
    }

    [Fact]
    public async Task PlainCallMustNotBeBlockedByOwnHubTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        await connection.ClientPeer.WhenConnected(Timeout);

        // The client and the server share one hub here, so IsOwnHub is true on every test
        // connection - the guard must still fire only for calls that asked for it
        connection.ClientPeer.InternalServices.Transport!.IsOwnHub.Should().BeTrue();
        var client = services.RpcHub().GetClient<IServeStaleTester>();
        (await client.Get("1").WaitAsync(Timeout)).Should().Be("v-1");
    }

    // Private methods

    private static async Task<(RpcClientPeer Peer, RpcOutboundCall Call)> NewOwnHubCall(IServiceProvider services)
    {
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var peer = connection.ClientPeer;
        await peer.WhenConnected(Timeout);
        peer.InternalServices.Transport!.IsOwnHub.Should().BeTrue();

        var method = services.RpcHub().ServiceRegistry[typeof(IServeStaleTester)]["Get:2"];
        var context = new RpcOutboundContext(peer) { MustNotCallOwnHub = true };
        var arguments = ArgumentList.New("1", default(CancellationToken));
        return (peer, context.PrepareCall(method, arguments)!);
    }
}
