namespace ActualLab.Rpc.WebSockets;

/// <summary>
/// Well-known WebSocket close codes used by the RPC framework.
/// Codes in the 4000–4999 range are reserved for application use per RFC 6455.
/// </summary>
public static class RpcWebSocketCloseCode
{
    /// <summary>Server doesn't support the client's requested serialization format.</summary>
    public const int UnsupportedFormat = 4001;
}
