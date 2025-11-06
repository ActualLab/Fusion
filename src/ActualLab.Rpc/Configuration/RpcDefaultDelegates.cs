using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using ActualLab.OS;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Rpc;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume RPC-related code is fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "We assume RPC-related code is fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume RPC-related code is fully preserved")]
public static class RpcDefaultDelegates
{
    private static readonly string KeepAliveMethodName = $"{nameof(IRpcSystemCalls.KeepAlive)}:1";

    // Configuration related

    public static RpcServiceDefBuilder ServiceDefBuilder { get; set; } =
        static (hub, service) => new RpcServiceDef(hub, service);

    public static RpcMethodDefBuilder MethodDefBuilder { get; set; } =
        static (service, method) => new RpcMethodDef(service, method);

    public static RpcServiceScopeResolver ServiceScopeResolver { get; set; } =
        static service => service.IsBackend
            ? RpcDefaults.BackendScope
            : RpcDefaults.ApiScope;

    public static RpcHashProvider HashProvider { get; set; } =
        static bytes => {
            // It's better to use a more efficient hash function here, e.g., Blake3.
            // We use SHA256 mainly to minimize the number of dependencies.
#if NET5_0_OR_GREATER
            var buffer = (Span<byte>)stackalloc byte[32]; // 32 bytes
            SHA256.HashData(bytes.Span, buffer);
            return Convert.ToBase64String(buffer[..18]); // 18 bytes -> 24 chars
#else
            using var sha256 = SHA256.Create();
            var buffer = sha256.ComputeHash(bytes.TryGetUnderlyingArray() ?? bytes.ToArray()); // 32 bytes
            return Convert.ToBase64String(buffer.AsSpan(0, 18).ToArray()); // 18 bytes -> 24 chars
#endif
        };

    // RpcInboundContext / RpcOutboundContext factories

    public static RpcInboundContextFactory InboundContextFactory { get; set; } =
        static (peer, message, peerChangedToken) => new RpcInboundContext(peer, message, peerChangedToken);

    // Call validation & filtering


    public static RpcInboundCallFilter InboundCallFilter { get; set; } =
        static (peer, method) => !method.IsBackend || peer.Ref.IsBackend;

    // Call routing

    // If you use Fusion, FusionBuilder injects FusionRpcDefaultDelegates.CallRouter instead of this delegate!
    public static RpcCallRouterFactory CallRouterFactory { get; set; }
        = static method => static args => RpcPeerRef.Default;

    public static RandomTimeSpan RerouteDelayerDelay { get; set; }
        = TimeSpan.FromMilliseconds(100).ToRandom(0.25);

    public static RpcRerouteDelayer RerouteDelayer { get; set; } =
        static cancellationToken => Task.Delay(RerouteDelayerDelay.Next(), cancellationToken);

    // Call timeouts

    public static RpcCallTimeoutsProvider CallTimeoutsProvider { get; set; } =
        method => {
            if (RpcCallTimeouts.Defaults.IsDebugEnabled && Debugger.IsAttached)
                return RpcCallTimeouts.Defaults.Debug;

            if (method.IsBackend)
                return method.Kind is RpcMethodKind.Command
                    ? RpcCallTimeouts.Defaults.BackendCommand
                    : RpcCallTimeouts.Defaults.BackendQuery;

            return method.Kind is RpcMethodKind.Command
                ? RpcCallTimeouts.Defaults.Command
                : RpcCallTimeouts.Defaults.Query;
        };

    // RpcPeer management

    public static RpcPeerFactory PeerFactory { get; set; } =
        static (hub, peerRef) => peerRef.IsServer
            ? new RpcServerPeer(hub, peerRef)
            : new RpcClientPeer(hub, peerRef);

    public static RpcPeerConnectionKindResolver PeerConnectionKindResolver { get; set; } =
        static (hub, peerRef) => peerRef.ConnectionKind;

    public static RpcPeerTerminalErrorDetector PeerTerminalErrorDetector { get; set; } =
        static error => error is RpcReconnectFailedException;

    // Server-side RPC peer management

    public static RpcServerConnectionFactory ServerConnectionFactory { get; set; } =
        static (peer, channel, options, cancellationToken) => Task.FromResult(new RpcConnection(channel, options));

    public static RpcServerPeerCloseTimeoutProvider ServerPeerCloseTimeoutProvider { get; set; } =
        static peer => {
            var peerLifetime = peer.CreatedAt.Elapsed;
            return peerLifetime.MultiplyBy(0.33).Clamp(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(15));
        };

    // WebSocket channel

    public static Func<RpcPeer, PropertyBag, RpcFrameDelayerFactory?> FrameDelayerProvider { get; set; } =
        RpcFrameDelayerProviders.None;

    public static RpcWebSocketChannelOptionsProvider WebSocketChannelOptionsProvider { get; set; } =
        static (peer, properties) => WebSocketChannel<RpcMessage>.Options.Default with {
            Serializer = peer.Hub.SerializationFormats.Get(peer.Ref).MessageSerializerFactory.Invoke(peer),
            FrameDelayerFactory = FrameDelayerProvider.Invoke(peer, properties),
        };

    // Call tracing and logging

    public static RpcCallTracerFactory CallTracerFactory { get; set; } =
        RuntimeInfo.IsServer
            ? static method => new RpcDefaultCallTracer(method)
            : static method => null; // To completely disable tracing and meters in RPC

    public static RpcCallLoggerFactory CallLoggerFactory { get; set; } =
        static (peer, filter, log, logLevel) => new RpcCallLogger(peer, filter, log, logLevel);

    public static RpcCallLoggerFilter CallLoggerFilter { get; set; } =
        static (peer, call) => {
            var methodDef = call.MethodDef;
            return !(methodDef.IsSystem && string.Equals(methodDef.Name, KeepAliveMethodName, StringComparison.Ordinal));
        };
}
