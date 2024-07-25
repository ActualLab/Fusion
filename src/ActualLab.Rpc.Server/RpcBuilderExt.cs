namespace ActualLab.Rpc.Server;

public static class RpcBuilderExt
{
    public static RpcWebSocketServerBuilder AddWebSocketServer(this RpcBuilder rpc)
        => new(rpc, null);

    public static RpcWebSocketServerBuilder AddWebSocketServer(this RpcBuilder rpc, bool exposeBackend)
    {
        var webSocketServer = new RpcWebSocketServerBuilder(rpc, null);
        if (exposeBackend)
            webSocketServer.Configure(static _ => RpcWebSocketServer.Options.Default with {
                ExposeBackend = true,
            });
        return webSocketServer;
    }

    public static RpcBuilder AddWebSocketServer(this RpcBuilder rpc, Action<RpcWebSocketServerBuilder> configure)
        => new RpcWebSocketServerBuilder(rpc, configure).Rpc;
}
