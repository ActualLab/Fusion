#if NETFRAMEWORK
using System.Net;
using System.Net.WebSockets;
using Microsoft.Owin;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Server;

namespace ActualLab.Tests.Audit;

public class RpcWebSocketServerNetFxAuditTest
{
    [Fact]
    public async Task AcceptedSocketUsesValidatedPeerRefAndIsDisposed()
    {
        var services = new ServiceCollection();
        var rpc = services.AddRpc();
        rpc.AddWebSocketServer();
        var peerRefFactoryCallCount = 0;
        services.AddSingleton<RpcWebSocketServerPeerRefFactory>((_, _, _) => {
            peerRefFactoryCallCount++;
            return RpcPeerRef.NewServer("audit", RpcSerializationFormat.SystemJsonV5.Key);
        });
        services.AddSingleton(new RpcPeerOptions {
            ServerConnectionFactory = (_, _, _, _) =>
                Task.FromException<RpcConnection>(new InvalidOperationException("Audit failure")),
        });
        using var serviceProvider = services.BuildServiceProvider();
        var server = serviceProvider.GetRequiredService<RpcWebSocketServer>();
        var context = new OwinContext();
        Func<IDictionary<string, object>, Task>? callback = null;
        context.Environment["websocket.Accept"] =
            new Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>(
                (_, value) => callback = value);
        context.Environment["owin.RequestHeaders"] = new Dictionary<string, string[]>();

        var status = await server.Invoke(context, isBackend: false);
        status.Should().Be(HttpStatusCode.SwitchingProtocols);
        callback.Should().NotBeNull();

        var webSocket = new AuditWebSocket();
        var webSocketContext = new AuditWebSocketContext(webSocket);
        await callback!(new Dictionary<string, object> {
            ["System.Net.WebSockets.WebSocketContext"] = webSocketContext,
        });

        peerRefFactoryCallCount.Should().Be(1);
        webSocket.IsDisposed.Should().BeTrue();
    }

    private sealed class AuditWebSocket : WebSocket
    {
        public bool IsDisposed { get; private set; }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => IsDisposed ? WebSocketState.Closed : WebSocketState.Open;
        public override string? SubProtocol => null;

        public override void Abort() => IsDisposed = true;

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            IsDisposed = true;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
            => CloseAsync(closeStatus, statusDescription, cancellationToken);

        public override void Dispose() => IsDisposed = true;

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
            => Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class AuditWebSocketContext(WebSocket webSocket) : WebSocketContext
    {
        public override CookieCollection CookieCollection { get; } = new();
        public override System.Collections.Specialized.NameValueCollection Headers { get; } = new();
        public override bool IsAuthenticated => false;
        public override bool IsLocal => true;
        public override bool IsSecureConnection => false;
        public override string Origin => "";
        public override Uri RequestUri { get; } = new("http://localhost/");
        public override string SecWebSocketKey => "";
        public override IEnumerable<string> SecWebSocketProtocols => [];
        public override string SecWebSocketVersion => "13";
        public override System.Security.Principal.IPrincipal User { get; }
            = new System.Security.Principal.GenericPrincipal(
                new System.Security.Principal.GenericIdentity(""), []);
        public override WebSocket WebSocket { get; } = webSocket;
    }
}
#endif
