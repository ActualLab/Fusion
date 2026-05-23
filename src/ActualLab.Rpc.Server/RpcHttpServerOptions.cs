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
    public bool UsePipes { get; init; } = true;
    public bool MustRequireHttp2 { get; init; } = true;
}
