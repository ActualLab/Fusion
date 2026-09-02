using ActualLab.Rpc.Clients;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Configuration options for <see cref="RpcHttpServer"/>, including
/// request paths, backend exposure, and connection parameters.
/// </summary>
public record RpcHttpServerOptions
{
    public static RpcHttpServerOptions Default { get; set; } = new();

    public bool ExposeBackend { get; init; } = false;
    public string RequestPath { get; init; } = RpcHttpClientOptions.Default.RequestPath;
    public string BackendRequestPath { get; init; } = RpcHttpClientOptions.Default.BackendRequestPath;
    public string SerializationFormatParameterName { get; init; } = RpcHttpClientOptions.Default.SerializationFormatParameterName;
    public string ClientIdParameterName { get; init; } = RpcHttpClientOptions.Default.ClientIdParameterName;
    public string ReconnectProofCounterParameterName { get; init; }
        = RpcHttpClientOptions.Default.ReconnectProofCounterParameterName;
    public string ReconnectProofParameterName { get; init; }
        = RpcHttpClientOptions.Default.ReconnectProofParameterName;

    // See the same option on RpcWebSocketServerOptions for the caveats: it requires sticky
    // routing and clients that all speak the reconnect proof protocol.
    public bool RequireReconnectProof { get; init; } = false;
    // See the same option on RpcWebSocketServerOptions
    public bool MustRejectOnApplicationStopping { get; init; } = true;
    public bool UsePipes { get; init; } = true;
    public bool MustRequireHttp2 { get; init; } = true;
}
