#if NETCOREAPP
using System.Net;
using System.Net.WebSockets;
using ActualLab.Rpc;
using ActualLab.Rpc.Internal;

namespace ActualLab.Tests.Rpc;

/// <summary>
/// Reconnect proof behaviour with a real, fully handshaken WebSocket client - in particular that
/// a barrage of unproven connects for the same <c>clientId</c> leaves the incumbent connection
/// completely undisturbed.
/// </summary>
public class RpcReconnectProofClientTest : RpcTestBase
{
    public RpcReconnectProofClientTest(ITestOutputHelper @out) : base(@out)
        => RequireReconnectProof = true;

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

    [Fact]
    public async Task ClientAdoptsTheSecretAndProvesOnEveryReconnect()
    {
        await ResetClientServices();
        await using var _ = await WebHost.Serve();
        var hub = ClientServices.RpcHub();
        var client = hub.GetClient<ITestRpcService>();
        (await client.Div(6, 2)).Should().Be(3);

        var clientPeer = hub.GetClientPeer(ClientPeerRef);
        var serverPeer = GetServerPeer(clientPeer.ClientId)!;
        clientPeer.ReconnectSecret.Should().NotBeNullOrEmpty();
        clientPeer.ReconnectSecret.Should().Be(serverPeer.ReconnectSecret);
        serverPeer.LastSeenReconnectCounter.Should().Be(0); // The very first connect can't carry a proof

        var lastCounter = 0L;
        for (var i = 0; i < 3; i++) {
            await clientPeer.Disconnect();
            (await client.Div(6, 2)).Should().Be(3);

            GetServerPeer(clientPeer.ClientId).Should().BeSameAs(serverPeer);
            serverPeer.LastSeenReconnectCounter.Should().BeGreaterThan(lastCounter);
            lastCounter = serverPeer.LastSeenReconnectCounter;
        }
    }

    [Fact]
    public async Task UnprovenConnectsCantEvictTheIncumbentConnection()
    {
        // The direct regression test for the eviction DoS: before the gate, any request carrying a
        // known clientId disconnected the incumbent before anything was verified.
        await ResetClientServices();
        await using var _ = await WebHost.Serve();
        var hub = ClientServices.RpcHub();
        var client = hub.GetClient<ITestRpcService>();
        (await client.Div(6, 2)).Should().Be(3);

        var clientPeer = hub.GetClientPeer(ClientPeerRef);
        var connectionState = clientPeer.ConnectionState;
        var serverPeer = GetServerPeer(clientPeer.ClientId)!;
        var peerCount = WebServices.RpcHub().InternalServices.Peers.Count;

        for (var i = 0; i < 50; i++) {
            var forgedProof = RpcReconnectProof.Compute(
                RpcReconnectProof.NewSecret(), clientPeer.ClientId, "1");
            (await RawConnect(clientPeer.ClientId)).Should().Be(HttpStatusCode.Forbidden);
            (await RawConnect(clientPeer.ClientId, "1", forgedProof)).Should().Be(HttpStatusCode.Forbidden);
        }

        // Same AsyncState instance means the peer never transitioned - not even to reconnect
        clientPeer.ConnectionState.Should().BeSameAs(connectionState);
        connectionState.Value.IsConnected().Should().BeTrue();
        (await client.Div(10, 2)).Should().Be(5);
        serverPeer.LastSeenReconnectCounter.Should().Be(0);
        WebServices.RpcHub().InternalServices.Peers.Count.Should().Be(peerCount);
    }

    [Fact]
    public async Task ReplayedConnectUrlIsSingleUse()
    {
        await ResetClientServices();
        await using var _ = await WebHost.Serve();
        var hub = ClientServices.RpcHub();
        var client = hub.GetClient<ITestRpcService>();
        (await client.Div(6, 2)).Should().Be(3);

        var clientPeer = hub.GetClientPeer(ClientPeerRef);
        var serverPeer = GetServerPeer(clientPeer.ClientId)!;
        var proof = RpcReconnectProof.Compute(serverPeer.ReconnectSecret, clientPeer.ClientId, "1");
        var connectionState = clientPeer.ConnectionState;

        // A captured URL works exactly once...
        (await RawConnect(clientPeer.ClientId, "1", proof)).Should().BeNull();
        serverPeer.LastSeenReconnectCounter.Should().Be(1);
        // ...and is spent from then on
        (await RawConnect(clientPeer.ClientId, "1", proof)).Should().Be(HttpStatusCode.Forbidden);
        serverPeer.LastSeenReconnectCounter.Should().Be(1);

        // The first (accepted) connect legitimately evicts the incumbent - it proved possession
        clientPeer.ConnectionState.Should().NotBeSameAs(connectionState);
        (await client.Div(10, 2)).Should().Be(5);
    }

    // Private methods

    private RpcServerPeer? GetServerPeer(string clientId)
    {
        var rpcRef = RpcRef.NewServer(clientId, SerializationFormat);
        return WebServices.RpcHub().TryGetServerPeer(rpcRef, out var peer) ? peer : null;
    }

    private async Task<HttpStatusCode?> RawConnect(
        string clientId, string? counterText = null, string? proof = null)
    {
        var query = $"?clientId={clientId}&f={SerializationFormat}";
        if (counterText is not null)
            query += $"&c={counterText}";
        if (proof is not null)
            query += $"&p={proof}";

        var uri = new Uri($"ws://{WebHost.ServerUri.Authority}/rpc/ws{query}");
        using var webSocket = new ClientWebSocket();
#if NET7_0_OR_GREATER
        webSocket.Options.CollectHttpResponseDetails = true;
#endif
        try {
            await webSocket.ConnectAsync(uri, CancellationToken.None);
            return null;
        }
        catch (WebSocketException e) {
            Out.WriteLine(e.Message);
#if NET7_0_OR_GREATER
            return webSocket.HttpStatusCode;
#else
            return e.Message.Contains("'403'") ? HttpStatusCode.Forbidden : HttpStatusCode.BadRequest;
#endif
        }
    }
}

/// <summary>
/// Negative control for <see cref="RpcReconnectProofClientTest"/>: with
/// <c>RequireReconnectProof = false</c> an unproven connect still takes the incumbent's place,
/// exactly as it did before the gate existed. This pins what flipping the flag actually buys.
/// </summary>
public class RpcReconnectProofDisabledTest(ITestOutputHelper @out) : RpcTestBase(@out)
{
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

    [Fact]
    public async Task UnprovenConnectStillEvictsTheIncumbent()
    {
        await ResetClientServices();
        await using var _ = await WebHost.Serve();
        var hub = ClientServices.RpcHub();
        var client = hub.GetClient<ITestRpcService>();
        (await client.Div(6, 2)).Should().Be(3);

        var clientPeer = hub.GetClientPeer(ClientPeerRef);
        var whenDisconnected = clientPeer.ConnectionState.Value.WhenDisconnected;
        var uri = new Uri($"ws://{WebHost.ServerUri.Authority}/rpc/ws"
            + $"?clientId={clientPeer.ClientId}&f={SerializationFormat}");

        using (var webSocket = new ClientWebSocket()) {
            await webSocket.ConnectAsync(uri, CancellationToken.None);
            await whenDisconnected.WaitAsync(TimeSpan.FromSeconds(10));
        }

        (await client.Div(10, 2)).Should().Be(5); // The legitimate client recovers by reconnecting
    }
}
#endif
