namespace ActualLab.Rpc.Server;

/// <summary>
/// Extension methods for <see cref="RpcBuilder"/> to add RPC WebSocket server support.
/// </summary>
public static class RpcBuilderExt
{
    public static RpcWebSocketServerBuilder AddWebSocketServer(this RpcBuilder rpc)
        => new(rpc, null);

    public static RpcWebSocketServerBuilder AddWebSocketServer(this RpcBuilder rpc, bool exposeBackend)
    {
        var webSocketServer = new RpcWebSocketServerBuilder(rpc, null);
        if (exposeBackend)
            webSocketServer.Configure(static _ => RpcWebSocketServerOptions.Default with {
                ExposeBackend = true,
            });
        return webSocketServer;
    }

    public static RpcBuilder AddWebSocketServer(this RpcBuilder rpc, Action<RpcWebSocketServerBuilder> configure)
        => new RpcWebSocketServerBuilder(rpc, configure).Rpc;
}
