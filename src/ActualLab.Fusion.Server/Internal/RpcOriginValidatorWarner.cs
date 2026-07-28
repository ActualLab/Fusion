using ActualLab.Fusion.Server.Rpc;
using ActualLab.Rpc;
using ActualLab.Rpc.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ActualLab.Fusion.Server.Internal;

// It lives here rather than in ActualLab.Rpc.Server because that assembly owns the knob,
// but only this one knows RPC connections carry the FusionAuth.SessionId cookie -
// which is what turns a missing Origin check into cross-site WebSocket hijacking.

/// <summary>
/// Logs a single startup warning when RPC WebSocket connections are session-bound
/// while nothing validates the <c>Origin</c> of the upgrade request.
/// </summary>
public sealed class RpcOriginValidatorWarner(IServiceProvider services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var peerOptions = services.GetRequiredService<RpcPeerOptions>();
        if (peerOptions.ServerConnectionFactory != RpcPeerOptionsExt.ServerConnectionFactory)
            return Task.CompletedTask;

        var serverOptions = services.GetRequiredService<RpcWebSocketServerOptions>();
        if (serverOptions.OriginValidator != RpcWebSocketServerOriginValidators.AllowAll)
            return Task.CompletedTask;

        // Options passed straight to app.UseWebSockets(...) are invisible here, so this catches
        // only the services.Configure<WebSocketOptions>(...) way of setting AllowedOrigins.
        if (services.GetService<IOptions<WebSocketOptions>>()?.Value is { AllowedOrigins.Count: > 0 })
            return Task.CompletedTask;

        services.LogFor(GetType()).LogWarning(
            "RpcWebSocketServerOptions.OriginValidator is RpcWebSocketServerOriginValidators.AllowAll, " +
            "but RPC connections are bound to the Fusion session cookie, so any web page can open one " +
            "carrying your users' sessions (cross-site WebSocket hijacking). " +
            "Set OriginValidator to RpcWebSocketServerOriginValidators.SameOrigin or .Allow(...), " +
            "or populate WebSocketOptions.AllowedOrigins. See docs/PartR-CO.md#rpcwebsocketserveroptions");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
