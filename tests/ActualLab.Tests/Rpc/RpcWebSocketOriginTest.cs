#if NETCOREAPP
using System.Net;
using System.Net.WebSockets;
using ActualLab.Rpc;
using ActualLab.Rpc.Server;

namespace ActualLab.Tests.Rpc;

[Trait("Category", "Rpc")]
public class RpcWebSocketOriginTest(ITestOutputHelper @out) : TestBase(@out)
{
    private const string ForeignOrigin = "https://evil.example.com";
    private const string AllowedOrigin = "https://app.example.com";
    // Connect resolves this to the test host's own origin, which is known only once the host exists
    private const string OwnOrigin = "<own>";

    [Fact]
    public async Task DefaultValidatorAllowsAnyOrigin()
    {
        var result = await Connect(null, ForeignOrigin);
        result.StatusCode.Should().BeNull();
        result.PeerCount.Should().Be(1);
    }

    [Fact]
    public async Task DefaultValidatorAllowsAbsentOrigin()
    {
        var result = await Connect(null, null);
        result.StatusCode.Should().BeNull();
        result.PeerCount.Should().Be(1);
    }

    [Fact]
    public async Task SameOriginRejectsForeignOriginAndCreatesNoPeer()
    {
        var result = await Connect(RpcWebSocketServerOriginValidators.SameOrigin, ForeignOrigin);
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.PeerCount.Should().Be(0);
    }

    [Fact]
    public async Task SameOriginAllowsAbsentOrigin()
    {
        var result = await Connect(RpcWebSocketServerOriginValidators.SameOrigin, null);
        result.StatusCode.Should().BeNull();
        result.PeerCount.Should().Be(1);
    }

    [Fact]
    public async Task SameOriginAllowsOwnOrigin()
    {
        var result = await Connect(RpcWebSocketServerOriginValidators.SameOrigin, OwnOrigin);
        result.StatusCode.Should().BeNull();
        result.PeerCount.Should().Be(1);
    }

    [Fact]
    public async Task SameOriginRejectsOpaqueNullOrigin()
    {
        var result = await Connect(RpcWebSocketServerOriginValidators.SameOrigin, "null");
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.PeerCount.Should().Be(0);
    }

    [Fact]
    public async Task AllowListRejectsForeignOriginAndCreatesNoPeer()
    {
        var result = await Connect(RpcWebSocketServerOriginValidators.Allow(AllowedOrigin), ForeignOrigin);
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.PeerCount.Should().Be(0);
    }

    [Fact]
    public async Task AllowListAllowsListedOrigin()
    {
        var result = await Connect(RpcWebSocketServerOriginValidators.Allow(AllowedOrigin), AllowedOrigin);
        result.StatusCode.Should().BeNull();
        result.PeerCount.Should().Be(1);
    }

    [Fact]
    public async Task AllowListAllowsAbsentOrigin()
    {
        var result = await Connect(RpcWebSocketServerOriginValidators.Allow(AllowedOrigin), null);
        result.StatusCode.Should().BeNull();
        result.PeerCount.Should().Be(1);
    }

    // These two pin down what ASP.NET Core's own WebSocketMiddleware does, since Fusion's
    // endpoint sits behind it: a non-empty AllowedOrigins list rejects a mismatched Origin
    // with 403 before the endpoint runs, but still lets a request without Origin through.

    [Fact]
    public async Task WebSocketMiddlewareRejectsForeignOrigin()
    {
        var result = await Connect(null, ForeignOrigin, [AllowedOrigin]);
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.PeerCount.Should().Be(0);
    }

    [Fact]
    public async Task WebSocketMiddlewareAllowsAbsentOrigin()
    {
        var result = await Connect(null, null, [AllowedOrigin]);
        result.StatusCode.Should().BeNull();
        result.PeerCount.Should().Be(1);
    }

    // Private methods

    private async Task<(HttpStatusCode? StatusCode, int PeerCount)> Connect(
        RpcWebSocketServerOriginValidator? originValidator,
        string? origin,
        string[]? webSocketAllowedOrigins = null)
    {
        var baseServices = new ServiceCollection();
        baseServices.AddLogging(logging => logging.ClearProviders());
        using var webHost = new RpcWebHost(baseServices, GetType().Assembly) {
            OriginValidator = originValidator,
            WebSocketAllowedOrigins = webSocketAllowedOrigins ?? [],
        };
        await using var _ = await webHost.Serve();

        var serverUri = webHost.ServerUri;
        if (origin == OwnOrigin)
            origin = $"http://{serverUri.Authority}";
        var uri = new Uri($"ws://{serverUri.Authority}/rpc/ws"
            + $"?clientId={Guid.NewGuid():N}&f={RpcTestBase.DefaultSerializationFormat}");

        HttpStatusCode? statusCode = null;
        using (var client = new ClientWebSocket()) {
#if NET7_0_OR_GREATER
            client.Options.CollectHttpResponseDetails = true;
#endif
            if (origin is not null)
                client.Options.SetRequestHeader("Origin", origin);
            try {
                await client.ConnectAsync(uri, CancellationToken.None);
            }
            catch (WebSocketException e) {
                WriteLine(e.Message);
#if NET7_0_OR_GREATER
                statusCode = client.HttpStatusCode;
#else
                statusCode = e.Message.Contains("'403'") ? HttpStatusCode.Forbidden : HttpStatusCode.BadRequest;
#endif
            }
        }

        var peerCount = webHost.Services.RpcHub().InternalServices.Peers.Count;
        return (statusCode, peerCount);
    }
}
#endif
