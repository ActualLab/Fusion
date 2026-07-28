using ActualLab.Rpc.Clients;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Configuration options for <see cref="RpcWebSocketServer"/>, including
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

    // Turn this on only once every client that can still reconnect to a live server peer speaks
    // the reconnect proof protocol, and only behind sticky routing: a client whose reconnect lands
    // on a replica that already holds a peer for its clientId - but issued a different secret -
    // is rejected until that peer expires (3-15 min). A host with a custom ConnectionUriResolver
    // that supplies a persistent clientId must not enable this unless it persists the secret too.
    // With false, an absent c/p pair takes the legacy path, but a present-yet-invalid one is
    // still rejected. Bindable from configuration, so it can be rolled back without a redeploy.
    public bool RequireReconnectProof { get; init; } = false;
    public RpcWebSocketServerOriginValidator OriginValidator { get; init; }
        = RpcWebSocketServerDefaultDelegates.OriginValidator;

    // Logs one startup warning when nothing validates the Origin of the upgrade request. Turn it
    // off for a server whose connections carry no ambient credentials - a backend-only endpoint,
    // or one where every call authenticates itself - since cross-site WebSocket hijacking needs
    // the browser to attach something the attacker doesn't have.
    public bool WarnOnUnvalidatedOrigin { get; init; } = true;
#if NET6_0_OR_GREATER
    public RpcWebSocketServerAcceptContextFactory ConfigureWebSocket { get; init; }
        = RpcWebSocketServerDefaultDelegates.AcceptContextFactory;
#endif
}
