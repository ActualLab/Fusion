using ActualLab.Interception;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public delegate RpcServiceDef RpcServiceDefBuilder(RpcHub hub, RpcServiceBuilder service);
public delegate RpcMethodDef RpcMethodDefBuilder(RpcServiceDef service, MethodInfo method);
public delegate bool RpcBackendServiceDetector(Type serviceType);
public delegate Symbol RpcServiceScopeResolver(RpcServiceDef serviceDef);
public delegate RpcPeer RpcCallRouter(RpcMethodDef method, ArgumentList arguments);
public delegate Task RpcRerouteDelayer(CancellationToken cancellationToken);
public delegate void RpcPeerTracker(RpcPeer peer);
public delegate RpcPeer RpcPeerFactory(RpcHub hub, RpcPeerRef peerRef);
public delegate RpcInboundContext RpcInboundContextFactory(
    RpcPeer peer, RpcMessage message, CancellationToken cancellationToken);
public delegate bool RpcInboundCallFilter(RpcPeer peer, RpcMethodDef method);
public delegate Task<RpcConnection> RpcClientConnectionFactory(
    RpcClientPeer peer, CancellationToken cancellationToken);
public delegate Task<RpcConnection> RpcServerConnectionFactory(
    RpcServerPeer peer, Channel<RpcMessage> channel, PropertyBag properties, CancellationToken cancellationToken);
public delegate bool RpcUnrecoverableErrorDetector(Exception error, CancellationToken cancellationToken);
public delegate RpcMethodTracer? RpcMethodTracerFactory(RpcMethodDef method);
public delegate RpcCallLogger RpcCallLoggerFactory(RpcPeer peer, RpcCallLoggerFilter filter, ILogger log, LogLevel logLevel);
public delegate bool RpcCallLoggerFilter(RpcPeer peer, RpcCall call);

public static class RpcDefaultDelegates
{
    public static RpcServiceDefBuilder ServiceDefBuilder { get; set; } =
        static (hub, service) => new RpcServiceDef(hub, service);

    public static RpcMethodDefBuilder MethodDefBuilder { get; set; } =
        static (service, method) => new RpcMethodDef(service, service.Type, method);

    public static RpcBackendServiceDetector BackendServiceDetector { get; set; } =
        static serviceType =>
            typeof(IBackendService).IsAssignableFrom(serviceType)
            || serviceType.Name.EndsWith("Backend", StringComparison.Ordinal);

    public static RpcServiceScopeResolver ServiceScopeResolver { get; set; } =
        static service => service.IsBackend
            ? RpcDefaults.BackendScope
            : RpcDefaults.ApiScope;

    // See also: RpcSafeCallRouter
    public static RpcCallRouter CallRouter { get; set; } =
        static (method, arguments) => method.Hub.GetPeer(RpcPeerRef.Default); // May throw RpcRerouteException!

    public static RandomTimeSpan RerouteDelayerDelay { get; set; } = TimeSpan.FromMilliseconds(100).ToRandom(0.25);

    public static RpcRerouteDelayer RerouteDelayer { get; set; } =
        static cancellationToken => Task.Delay(RerouteDelayerDelay.Next(), cancellationToken);

    public static RpcPeerFactory PeerFactory { get; set; } =
        static (hub, peerRef) => peerRef.IsServer
            ? new RpcServerPeer(hub, peerRef)
            : new RpcClientPeer(hub, peerRef);

    public static RpcInboundContextFactory InboundContextFactory { get; set; } =
        static (peer, message, cancellationToken) => new RpcInboundContext(peer, message, cancellationToken);

    public static RpcInboundCallFilter InboundCallFilter { get; set; } =
        static (peer, method) => !method.IsBackend || peer.Ref.IsBackend;

    public static RpcClientConnectionFactory ClientConnectionFactory { get; set; } =
#pragma warning disable IL2026
        static (peer, cancellationToken) => peer.Hub.Client.Connect(peer, cancellationToken);
#pragma warning restore IL2026

    public static RpcServerConnectionFactory ServerConnectionFactory { get; set; } =
        static (peer, channel, options, cancellationToken) => Task.FromResult(new RpcConnection(channel, options));

    public static RpcUnrecoverableErrorDetector UnrecoverableErrorDetector { get; set; } =
        static (error, cancellationToken)
            => cancellationToken.IsCancellationRequested
            || error is ConnectionUnrecoverableException;

    public static RpcMethodTracerFactory MethodTracerFactory { get; set; } =
        static method => null;

    public static RpcCallLoggerFactory CallLoggerFactory { get; set; } =
        static (peer, filter, log, logLevel) => new RpcCallLogger(peer, filter, log, logLevel);

    private static readonly Symbol KeepAliveMethodName = (Symbol)$"{nameof(IRpcSystemCalls.KeepAlive)}:1";
    public static RpcCallLoggerFilter CallLoggerFilter { get; set; } =
        static (peer, call) => {
            var methodDef = call.MethodDef;
            return !(methodDef.IsSystem && methodDef.Name == KeepAliveMethodName);
        };
}
