using ActualLab.Rpc.Clients;
using Microsoft.AspNetCore.Http;

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
    public TimeSpan ChangeConnectionDelay { get; init; } = TimeSpan.FromSeconds(0.5);
#if NET6_0_OR_GREATER
    public Func<WebSocketAcceptContext> ConfigureWebSocket { get; init; } = () => new();
#endif
}
