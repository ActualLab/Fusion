using ActualLab.Rpc.Clients;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Configuration options for the OWIN-based <see cref="RpcWebSocketServer"/>, including
/// request paths, backend exposure, and connection parameters.
/// </summary>
public record RpcWebSocketServerOptions
{
    public static RpcWebSocketServerOptions Default { get; set; } = new();

    public bool ExposeBackend { get; init; } = false;
    public string RequestPath { get; init; } = RpcWebSocketClientOptions.Default.RequestPath;
    public string BackendRequestPath { get; init; } = RpcWebSocketClientOptions.Default.BackendRequestPath;
    public string SerializationFormatParameterName { get; init; } = RpcWebSocketClientOptions.Default.SerializationFormatParameterName;
    public string ClientIdParameterName { get; init; } = RpcWebSocketClientOptions.Default.ClientIdParameterName;
    public string ReconnectProofCounterParameterName { get; init; }
        = RpcWebSocketClientOptions.Default.ReconnectProofCounterParameterName;
    public string ReconnectProofParameterName { get; init; }
        = RpcWebSocketClientOptions.Default.ReconnectProofParameterName;

    // See the same option on ActualLab.Rpc.Server's RpcWebSocketServerOptions for the caveats:
    // it requires sticky routing and clients that all speak the reconnect proof protocol.
    public bool RequireReconnectProof { get; init; } = false;
    public RpcWebSocketServerOriginValidator OriginValidator { get; init; }
        = RpcWebSocketServerDefaultDelegates.OriginValidator;

    // See the same option on ActualLab.Rpc.Server's RpcWebSocketServerOptions.
    public bool WarnOnUnvalidatedOrigin { get; init; } = true;
    public RpcWebSocketServerAcceptContextFactory ConfigureWebSocket { get; init; }
        = RpcWebSocketServerDefaultDelegates.AcceptContextFactory;
}
