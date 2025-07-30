using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using ActualLab.Interception;
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
    private static readonly ConcurrentDictionary<Type, bool> IsCommandTypeCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static string CommandInterfaceFullName { get; set; } = "ActualLab.CommandR.ICommand";

    public static RpcServiceDefBuilder ServiceDefBuilder { get; set; } =
        static (hub, service) => new RpcServiceDef(hub, service);

    public static RpcMethodDefBuilder MethodDefBuilder { get; set; } =
        static (service, method) => new RpcMethodDef(service, service.Type, method);

    public static RpcBackendServiceDetector BackendServiceDetector { get; set; } =
        static serviceType =>
            typeof(IBackendService).IsAssignableFrom(serviceType)
            || serviceType.Name.EndsWith("Backend", StringComparison.Ordinal);

    public static RpcCommandTypeDetector CommandTypeDetector { get; set; } =
        static type => IsCommandTypeCache.GetOrAdd(type,
            static t => t.GetInterfaces().Any(x => CommandInterfaceFullName.Equals(x.FullName, StringComparison.Ordinal)));

    public static RpcCallTimeoutsProvider CallTimeoutsProvider { get; set; } =
        method => {
            if (RpcCallTimeouts.Defaults.IsDebugEnabled && Debugger.IsAttached)
                return RpcCallTimeouts.Defaults.Debug;

            if (method.IsBackend)
                return method.IsCommand
                    ? RpcCallTimeouts.Defaults.BackendCommand
                    : RpcCallTimeouts.Defaults.BackendQuery;

            return method.IsCommand
                ? RpcCallTimeouts.Defaults.Command
                : RpcCallTimeouts.Defaults.Query;
        };

    public static RpcCallValidatorProvider CallValidatorProvider { get; set; } =
        static method => {
#if NET6_0_OR_GREATER // NullabilityInfoContext is available in .NET 6.0+
            if (!RpcDefaults.UseCallValidator)
                return null;
            if (method.NoWait || method.IsSystem)
                return null; // These methods are supposed to rely on built-in validation for perf. reasons

            var nonNullableArgIndexesList = new List<int>();
            var nullabilityInfoContext = new NullabilityInfoContext();
            var parameters = method.Parameters;
            for (var i = 0; i < parameters.Length; i++) {
                var p = parameters[i];
                if (p.ParameterType.IsClass && nullabilityInfoContext.Create(p).ReadState == NullabilityState.NotNull)
                    nonNullableArgIndexesList.Add(i);
            }
            if (nonNullableArgIndexesList.Count == 0)
                return null;

            var nonNullableArgIndexes = nonNullableArgIndexesList.ToArray();
            return call => {
                var args = call.Arguments!;
                foreach (var index in nonNullableArgIndexes)
                    ArgumentNullException.ThrowIfNull(args.GetUntyped(index), parameters[index].Name);
            };
#else
            return null;
#endif
        };

    public static RpcServiceScopeResolver ServiceScopeResolver { get; set; } =
        static service => service.IsBackend
            ? RpcDefaults.BackendScope
            : RpcDefaults.ApiScope;

    // See also: RpcSafeCallRouter
    public static RpcCallRouter CallRouter { get; set; } =
        static (method, arguments) => RpcPeerRef.Default;

    public static RpcHashProvider HashProvider { get; set; } =
        static data => {
            // It's better to use more efficient hash function here, e.g. Blake3.
            // We use SHA256 mainly to minimize the number of dependencies.
#if NET5_0_OR_GREATER
            var bytes = (Span<byte>)stackalloc byte[32]; // 32 bytes
            SHA256.HashData(data.Data.Span, bytes);
            return Convert.ToBase64String(bytes[..18]); // 18 bytes -> 24 chars
#else
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(data.Bytes); // 32 bytes
            return Convert.ToBase64String(bytes.AsSpan(0, 18).ToArray()); // 18 bytes -> 24 chars
#endif
        };

    public static RandomTimeSpan RerouteDelayerDelay { get; set; }
        = TimeSpan.FromMilliseconds(100).ToRandom(0.25);

    public static RpcRerouteDelayer RerouteDelayer { get; set; } =
        static cancellationToken => Task.Delay(RerouteDelayerDelay.Next(), cancellationToken);

    public static RpcPeerFactory PeerFactory { get; set; } =
        static (hub, peerRef) => peerRef.IsServer
            ? new RpcServerPeer(hub, peerRef)
            : new RpcClientPeer(hub, peerRef);

    public static RpcInboundContextFactory InboundContextFactory { get; set; } =
        static (peer, message, peerChangedToken) => new RpcInboundContext(peer, message, peerChangedToken);

    public static RpcInboundCallFilter InboundCallFilter { get; set; } =
        static (peer, method) => !method.IsBackend || peer.Ref.IsBackend;

    public static RpcServerConnectionFactory ServerConnectionFactory { get; set; } =
        static (peer, channel, options, cancellationToken) => Task.FromResult(new RpcConnection(channel, options));

    public static Func<RpcPeer, PropertyBag, RpcFrameDelayerFactory?> FrameDelayerProvider { get; set; } =
        RpcFrameDelayerProviders.None;

    public static RpcWebSocketChannelOptionsProvider WebSocketChannelOptionsProvider { get; set; } =
        static (peer, properties) => WebSocketChannel<RpcMessage>.Options.Default with {
            Serializer = peer.Hub.SerializationFormats.Get(peer.Ref).MessageSerializerFactory.Invoke(peer),
            FrameDelayerFactory = FrameDelayerProvider.Invoke(peer, properties),
        };

    public static RpcServerPeerCloseTimeoutProvider ServerPeerCloseTimeoutProvider { get; set; } =
        static peer => {
            var peerLifetime = peer.CreatedAt.Elapsed;
            return peerLifetime.MultiplyBy(0.33).Clamp(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(15));
        };

    public static RpcPeerTerminalErrorDetector PeerTerminalErrorDetector { get; set; } =
        static error => error is RpcReconnectFailedException;

    public static RpcCallTracerFactory CallTracerFactory { get; set; } =
        static method => new RpcDefaultCallTracer(method, traceOutbound: method.IsBackend);
        // static method => null; // To completely disable tracing and meters in RPC

    public static RpcCallLoggerFactory CallLoggerFactory { get; set; } =
        static (peer, filter, log, logLevel) => new RpcCallLogger(peer, filter, log, logLevel);

    private static readonly string KeepAliveMethodName = $"{nameof(IRpcSystemCalls.KeepAlive)}:1";
    public static RpcCallLoggerFilter CallLoggerFilter { get; set; } =
        static (peer, call) => {
            var methodDef = call.MethodDef;
            return !(methodDef.IsSystem && string.Equals(methodDef.Name, KeepAliveMethodName, StringComparison.Ordinal));
        };
}
