#if NET5_0_OR_GREATER
using System.Net;
using ActualLab.Rpc;
using ActualLab.Rpc.Clients.Internal;

namespace ActualLab.Tests.Rpc;

// Regression tests for the HTTP transport's connection reuse.
//
// Every RpcHttpClient connection keeps its request open for as long as the connection lives, and
// SocketsHttpHandler can't carry two such requests over one HTTP/2 connection - the second one never
// receives its response headers. RpcHttpClient used to share a single HttpClient across connections,
// so a peer could never reconnect: every attempt timed out with "Timeout while connecting to remote
// host" forever, which hung whichever test happened to trigger a reconnect.
public class RpcHttpReconnectTest : RpcTestBase
{
    // Generous enough for a slow CI box, small enough to fail instead of hanging the run
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(20);

    public RpcHttpReconnectTest(ITestOutputHelper @out) : base(@out)
    {
        UseHttpClient = true;
        ExposeBackend = true;
    }

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
    public async Task ReconnectTest()
    {
        await ResetClientServices();
        await using var _ = await WebHost.Serve();
        var client = ClientServices.RpcHub().GetClient<ITestRpcService>();
        var peer = ClientServices.RpcHub().GetClientPeer(ClientPeerRef);

        (await Call(client.Div(6, 2))).Should().Be(3);
        for (var i = 0; i < 3; i++) {
            await peer.Disconnect().ConfigureAwait(false);
            (await Call(client.Div(6, 2))).Should().Be(3);
        }
    }

    // Two peers of the same client talk to the same host at the same time, which is what
    // ExposeBackend-style setups do. Both connections must work concurrently.
    [Fact]
    public async Task TwoConcurrentPeersTest()
    {
        await ResetClientServices();
        await using var _ = await WebHost.Serve();
        var hub = ClientServices.RpcHub();
        var client = hub.GetClient<ITestRpcService>();

        (await Call(client.Div(6, 2))).Should().Be(3);
        var backendPeer = hub.GetClientPeer(BackendClientPeerRef);
        await Call(backendPeer.WhenConnected(CancellationToken.None));
        (await Call(client.Div(10, 2))).Should().Be(5);
    }

    // The underlying constraint, without any RPC: a second open-ended duplex POST must not be
    // starved by the first one. RpcHttpClient satisfies this by giving each connection its own
    // HttpClient - if that ever regresses, this test says why.
    [Fact]
    public async Task ConcurrentDuplexPostsNeedSeparateHttpClientsTest()
    {
        await using var _ = await WebHost.Serve();
        var uri = WebHost.ServerUri;
        using var httpClient1 = new HttpClient(new SocketsHttpHandler());
        using var httpClient2 = new HttpClient(new SocketsHttpHandler());

        var (status1, content1) = await SendDuplexPost(httpClient1, uri).ConfigureAwait(false);
        var (status2, content2) = await SendDuplexPost(httpClient2, uri).ConfigureAwait(false);
        content1.Complete();
        content2.Complete();

        // 404: the root path isn't the RPC endpoint - what matters is that both got a response
        status1.Should().Be(HttpStatusCode.NotFound);
        status2.Should().Be(HttpStatusCode.NotFound);
    }

    // Private methods

    private static async Task<T> Call<T>(Task<T> task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(CallTimeout)).ConfigureAwait(false);
        if (completed != task)
            throw new TimeoutException($"The call didn't complete in {CallTimeout.ToShortString()}.");

        return await task.ConfigureAwait(false);
    }

    private static async Task Call(Task task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(CallTimeout)).ConfigureAwait(false);
        if (completed != task)
            throw new TimeoutException($"The call didn't complete in {CallTimeout.ToShortString()}.");

        await task.ConfigureAwait(false);
    }

    private static async Task<(HttpStatusCode Status, DuplexHttpContent Content)> SendDuplexPost(
        HttpClient httpClient, Uri uri)
    {
        var content = new DuplexHttpContent();
        var request = new HttpRequestMessage(HttpMethod.Post, uri) {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = content,
        };
        using var cts = new CancellationTokenSource(CallTimeout);
        var response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
            .ConfigureAwait(false);
        return (response.StatusCode, content);
    }
}
#endif
