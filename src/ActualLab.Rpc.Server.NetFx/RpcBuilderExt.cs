namespace ActualLab.Rpc.Server;

/// <summary>
/// Extension methods for <see cref="RpcBuilder"/> to add RPC WebSocket server support
/// for the .NET Framework (OWIN) hosting model.
/// </summary>
public static class RpcBuilderExt
{
    public static RpcWebSocketServerBuilder AddWebSocketServer(this RpcBuilder rpc)
        => new(rpc, null);

    public static RpcBuilder AddWebSocketServer(this RpcBuilder rpc, Action<RpcWebSocketServerBuilder> configure)
        => new RpcWebSocketServerBuilder(rpc, configure).Rpc;
}
