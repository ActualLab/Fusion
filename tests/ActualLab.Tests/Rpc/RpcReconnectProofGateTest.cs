#if NETCOREAPP
using System.Globalization;
using System.Net;
using System.Net.WebSockets;
using ActualLab.Rpc;
using ActualLab.Rpc.Internal;

namespace ActualLab.Tests.Rpc;

/// <summary>
/// End-to-end coverage of the reconnect proof gate on the ASP.NET Core WebSocket and HTTP/2
/// endpoints. The OWIN endpoint shares the very same policy through
/// <see cref="RpcReconnectProof.TryVerify"/>, which <see cref="RpcReconnectProofTest"/> covers
/// directly - an OWIN host can't be started under this TFM.
/// </summary>
[Trait("Category", "Rpc")]
public class RpcReconnectProofGateTest(ITestOutputHelper @out) : TestBase(@out)
{
    private static readonly string Format = RpcTestBase.DefaultSerializationFormat;

    [Fact]
    public async Task UnknownClientIdConnectsWithoutProof()
    {
        await using var host = await NewHost(requireReconnectProof: true);

        var status = await host.Connect(NewClientId());

        status.Should().BeNull();
        host.PeerCount.Should().Be(1);
    }

    [Fact]
    public async Task UnknownClientIdConnectsWithGarbageProof()
    {
        // D8: this is exactly what a client that reached a different replica sends
        await using var host = await NewHost(requireReconnectProof: true);

        var status = await host.Connect(NewClientId(), "not-a-number", "!!!");

        status.Should().BeNull();
        host.PeerCount.Should().Be(1);
    }

    [Fact]
    public async Task ServerIssuesASecretToTheConnectedPeer()
    {
        await using var host = await NewHost(requireReconnectProof: true);
        var clientId = NewClientId();

        await host.Connect(clientId);

        var peer = host.GetServerPeer(clientId);
        peer.Should().NotBeNull();
        peer!.Secret.Should().MatchRegex("^[A-Za-z0-9_-]{43}$");
        peer.LastCounter.Should().Be(0);
    }

    [Fact]
    public async Task ValidProofConnectsAndAdvancesTheCounter()
    {
        await using var host = await NewHost(requireReconnectProof: true);
        var clientId = NewClientId();
        await host.Connect(clientId);
        var peer = host.GetServerPeer(clientId)!;

        var status = await host.ConnectWithProof(clientId, peer.Secret, 7);

        status.Should().BeNull();
        peer.LastCounter.Should().Be(7);
    }

    [Theory]
    [InlineData(7)] // Replay of the exact counter already used
    [InlineData(3)] // Older counter
    public async Task ReplayedCounterIsRejected(long counter)
    {
        await using var host = await NewHost(requireReconnectProof: true);
        var clientId = NewClientId();
        await host.Connect(clientId);
        var peer = host.GetServerPeer(clientId)!;
        (await host.ConnectWithProof(clientId, peer.Secret, 7)).Should().BeNull();
        var peerCount = host.PeerCount;

        var status = await host.ConnectWithProof(clientId, peer.Secret, counter);

        status.Should().Be(HttpStatusCode.Forbidden);
        peer.LastCounter.Should().Be(7);
        host.PeerCount.Should().Be(peerCount);
    }

    [Fact]
    public async Task ForgedProofIsRejected()
    {
        await using var host = await NewHost(requireReconnectProof: true);
        var clientId = NewClientId();
        await host.Connect(clientId);
        var peer = host.GetServerPeer(clientId)!;

        var status = await host.ConnectWithProof(clientId, RpcReconnectProof.NewSecret(), 7);

        status.Should().Be(HttpStatusCode.Forbidden);
        peer.LastCounter.Should().Be(0);
    }

    [Fact]
    public async Task TamperedProofIsRejected()
    {
        await using var host = await NewHost(requireReconnectProof: true);
        var clientId = NewClientId();
        await host.Connect(clientId);
        var peer = host.GetServerPeer(clientId)!;
        var proof = RpcReconnectProof.Compute(peer.Secret, clientId, "7");
        var tamperedProof = (proof[0] == 'a' ? 'b' : 'a') + proof[1..];

        var status = await host.Connect(clientId, "7", tamperedProof);

        status.Should().Be(HttpStatusCode.Forbidden);
        peer.LastCounter.Should().Be(0);
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("tooShort")]
    public async Task UndecodableProofIsRejected(string proof)
    {
        await using var host = await NewHost(requireReconnectProof: true);
        var clientId = NewClientId();
        await host.Connect(clientId);

        var status = await host.Connect(clientId, "7", proof);

        status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HalfPresentProofPairIsRejected()
    {
        await using var host = await NewHost(requireReconnectProof: false);
        var clientId = NewClientId();
        await host.Connect(clientId);
        var peer = host.GetServerPeer(clientId)!;
        var proof = RpcReconnectProof.Compute(peer.Secret, clientId, "7");

        (await host.Connect(clientId, "7", null)).Should().Be(HttpStatusCode.Forbidden);
        (await host.Connect(clientId, null, proof)).Should().Be(HttpStatusCode.Forbidden);
        peer.LastCounter.Should().Be(0);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1e3")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    public async Task NonCanonicalCounterIsRejected(string counterText)
    {
        await using var host = await NewHost(requireReconnectProof: true);
        var clientId = NewClientId();
        await host.Connect(clientId);
        var peer = host.GetServerPeer(clientId)!;
        var proof = RpcReconnectProof.Compute(peer.Secret, clientId, counterText);

        var status = await host.Connect(clientId, counterText, proof);

        status.Should().Be(HttpStatusCode.Forbidden);
        peer.LastCounter.Should().Be(0);
    }

    [Fact]
    public async Task LegacyClientConnectsWhenProofIsNotRequired()
    {
        await using var host = await NewHost(requireReconnectProof: false);
        var clientId = NewClientId();
        await host.Connect(clientId);

        var status = await host.Connect(clientId);

        status.Should().BeNull();
    }

    [Fact]
    public async Task LegacyClientIsRejectedWhenProofIsRequired()
    {
        await using var host = await NewHost(requireReconnectProof: true);
        var clientId = NewClientId();
        await host.Connect(clientId);

        var status = await host.Connect(clientId);

        status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RejectedRequestsCreateNoPeerAndBurnNoCounterSpace()
    {
        // Half of the eviction-DoS regression test: a request that fails the gate must not reach
        // GetServerPeer at all. RpcReconnectProofClientTest covers the other half - that a
        // genuinely connected incumbent survives the same barrage.
        await using var host = await NewHost(requireReconnectProof: true);
        var clientId = NewClientId();
        await host.Connect(clientId);
        var peer = host.GetServerPeer(clientId)!;
        var peerCount = host.PeerCount;

        for (var i = 0; i < 50; i++) {
            var status = await host.ConnectWithProof(clientId, RpcReconnectProof.NewSecret(), i + 1);
            status.Should().Be(HttpStatusCode.Forbidden);
        }

        host.PeerCount.Should().Be(peerCount);
        host.GetServerPeer(clientId).Should().BeSameAs(peer);
        peer.LastCounter.Should().Be(0);
    }

    [Fact]
    public async Task HttpEndpointGateBehavesTheSameWay()
    {
        await using var host = await NewHost(requireReconnectProof: true, useHttp: true);
        var clientId = NewClientId();
        (await host.HttpConnect(clientId)).Should().Be(HttpStatusCode.OK);
        var peer = host.GetServerPeer(clientId)!;

        var proof = RpcReconnectProof.Compute(peer.Secret, clientId, "5");
        (await host.HttpConnect(clientId, "5", proof)).Should().Be(HttpStatusCode.OK);
        peer.LastCounter.Should().Be(5);

        (await host.HttpConnect(clientId, "5", proof)).Should().Be(HttpStatusCode.Forbidden); // Replay
        (await host.HttpConnect(clientId)).Should().Be(HttpStatusCode.Forbidden); // No proof
        (await host.HttpConnect(clientId, "6", proof)).Should().Be(HttpStatusCode.Forbidden); // Wrong counter
        peer.LastCounter.Should().Be(5);
    }

    // Private methods

    private static string NewClientId()
        => Guid.NewGuid().ToString("N");

    private async Task<TestHost> NewHost(bool requireReconnectProof, bool useHttp = false)
    {
        var baseServices = new ServiceCollection();
        baseServices.AddLogging(logging => logging.ClearProviders());
        // useHttpClient makes Kestrel listen with HttpProtocols.Http2, which the HTTP/2 endpoint
        // requires and the WebSocket one can't use - so a host serves one or the other, not both.
        var webHost = new RpcWebHost(baseServices, GetType().Assembly, useHttp) {
            RequireReconnectProof = requireReconnectProof,
        };
        var serving = await webHost.Serve();
        return new TestHost(webHost, serving, Out);
    }

    // Nested types

    private sealed class TestHost(RpcWebHost webHost, IAsyncDisposable serving, ITestOutputHelper @out)
        : IAsyncDisposable
    {
        public int PeerCount => webHost.Services.RpcHub().InternalServices.Peers.Count;

        public async ValueTask DisposeAsync()
        {
            await serving.DisposeAsync();
            webHost.Dispose();
        }

        public RpcServerPeer? GetServerPeer(string clientId)
        {
            var rpcRef = RpcRef.NewServer(clientId, Format);
            return webHost.Services.RpcHub().TryGetServerPeer(rpcRef, out var peer) ? peer : null;
        }

        public Task<HttpStatusCode?> ConnectWithProof(string clientId, string secret, long counter)
        {
            var counterText = counter.ToString(CultureInfo.InvariantCulture);
            return Connect(clientId, counterText, RpcReconnectProof.Compute(secret, clientId, counterText));
        }

        public async Task<HttpStatusCode?> Connect(string clientId, string? counterText = null, string? proof = null)
        {
            var uri = new Uri($"ws://{webHost.ServerUri.Authority}/rpc/ws{BuildQuery(clientId, counterText, proof)}");
            using var client = new ClientWebSocket();
#if NET7_0_OR_GREATER
            client.Options.CollectHttpResponseDetails = true;
#endif
            try {
                await client.ConnectAsync(uri, CancellationToken.None);
                return null;
            }
            catch (WebSocketException e) {
                @out.WriteLine(e.Message);
#if NET7_0_OR_GREATER
                return client.HttpStatusCode;
#else
                return e.Message.Contains("'403'") ? HttpStatusCode.Forbidden : HttpStatusCode.BadRequest;
#endif
            }
        }

        public async Task<HttpStatusCode> HttpConnect(string clientId, string? counterText = null, string? proof = null)
        {
            // The endpoint holds the response open for the connection's lifetime, so
            // ResponseHeadersRead plus an immediate dispose is what "did the gate pass?" looks like.
            var uri = new Uri($"{webHost.ServerUri}rpc/http{BuildQuery(clientId, counterText, proof)}");
            using var httpClient = new HttpClient(new SocketsHttpHandler());
            using var request = new HttpRequestMessage(HttpMethod.Post, uri) {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                Content = new StreamContent(new MemoryStream()),
            };
            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            return response.StatusCode;
        }

        private static string BuildQuery(string clientId, string? counterText, string? proof)
        {
            var query = $"?clientId={clientId}&f={Format}";
            if (counterText is not null)
                query += $"&c={Uri.EscapeDataString(counterText)}";
            if (proof is not null)
                query += $"&p={Uri.EscapeDataString(proof)}";
            return query;
        }
    }
}
#endif
