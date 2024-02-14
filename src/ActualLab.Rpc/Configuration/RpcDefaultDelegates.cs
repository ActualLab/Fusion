using ActualLab.Interception;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public delegate RpcServiceDef RpcServiceDefBuilder(RpcHub hub, RpcServiceBuilder service);
public delegate RpcMethodDef RpcMethodDefBuilder(RpcServiceDef service, MethodInfo method);
public delegate bool RpcBackendServiceDetector(Type serviceType);
public delegate RpcPeer? RpcCallRouter(RpcMethodDef method, ArgumentList arguments);
public delegate void RpcPeerTracker(RpcPeer peer);
public delegate RpcPeer RpcPeerFactory(RpcHub hub, RpcPeerRef peerRef);
public delegate RpcInboundContext RpcInboundContextFactory(
    RpcPeer peer, RpcMessage message, CancellationToken cancellationToken);
public delegate bool RpcInboundCallFilter(RpcPeer peer, RpcMethodDef method);
public delegate Task<RpcConnection> RpcClientConnectionFactory(
    RpcClientPeer peer, CancellationToken cancellationToken);
public delegate Task<RpcConnection> RpcServerConnectionFactory(
    RpcServerPeer peer, Channel<RpcMessage> channel, ImmutableOptionSet options, CancellationToken cancellationToken);
public delegate bool RpcUnrecoverableErrorDetector(Exception error, CancellationToken cancellationToken);
public delegate RpcMethodTracer? RpcMethodTracerFactory(RpcMethodDef method);

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

    public static RpcCallRouter CallRouter { get; set; } =
        static (method, arguments) => method.Hub.GetPeer(RpcPeerRef.Default);

    public static RpcPeerFactory PeerFactory { get; set; } =
        static (hub, peerRef) => peerRef.IsServer
            ? new RpcServerPeer(hub, peerRef)
            : new RpcClientPeer(hub, peerRef);

    public static RpcInboundContextFactory InboundContextFactory { get; set; } =
#pragma warning disable IL2026
        static (peer, message, cancellationToken) => new RpcInboundContext(peer, message, cancellationToken);
#pragma warning restore IL2026

    public static RpcInboundCallFilter InboundCallFilter { get; set; } =
        static (peer, method) => !method.Service.IsBackend || peer.Ref.IsBackend;

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
}
