using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc.Internal;

public sealed class RpcInternalServices(RpcHub hub)
{
    private RpcClientInterceptor.Options? _clientInterceptorOptions;
    private RpcRoutingInterceptor.Options? _routingInterceptorOptions;

    private RpcClientInterceptor.Options ClientInterceptorOptions
        => _clientInterceptorOptions ??= Hub.Services.GetRequiredService<RpcClientInterceptor.Options>();
    private RpcRoutingInterceptor.Options RoutingInterceptorOptions
        => _routingInterceptorOptions ??= Hub.Services.GetRequiredService<RpcRoutingInterceptor.Options>();

    public RpcHub Hub = hub;
    public RpcServiceDefBuilder ServiceDefBuilder => Hub.ServiceDefBuilder;
    public RpcMethodDefBuilder MethodDefBuilder => Hub.MethodDefBuilder;
    public RpcServiceScopeResolver ServiceScopeResolver => Hub.ServiceScopeResolver;
    public RpcCallRouter CallRouter => Hub.CallRouter;
    public RpcArgumentSerializer ArgumentSerializer => Hub.ArgumentSerializer;
    public RpcInboundContextFactory InboundContextFactory => Hub.InboundContextFactory;
    public RpcInboundMiddlewares InboundMiddlewares => Hub.InboundMiddlewares;
    public RpcOutboundMiddlewares OutboundMiddlewares => Hub.OutboundMiddlewares;
    public RpcPeerFactory PeerFactory => Hub.PeerFactory;
    public RpcClientConnectionFactory ClientConnectionFactory => Hub.ClientConnectionFactory;
    public RpcClientPeerReconnectDelayer ClientPeerReconnectDelayer => Hub.ClientPeerReconnectDelayer;
    public RpcUnrecoverableErrorDetector UnrecoverableErrorDetector => Hub.UnrecoverableErrorDetector;
    public RpcMethodTracerFactory MethodTracerFactory => Hub.MethodTracerFactory;
    public RpcCallLoggerFactory CallLoggerFactory => Hub.CallLoggerFactory;
    public RpcCallLoggerFilter CallLoggerFilter => Hub.CallLoggerFilter;
    public IEnumerable<RpcPeerTracker> PeerTrackers => Hub.PeerTrackers;
    public RpcSystemCallSender SystemCallSender => Hub.SystemCallSender;
    public RpcClient Client => Hub.Client;

    public ConcurrentDictionary<RpcPeerRef, RpcPeer> Peers => Hub.Peers;

    public RpcClientInterceptor NewClientInterceptor(RpcServiceDef serviceDef, object? localService)
        => new(ClientInterceptorOptions, Hub.Services, serviceDef) {
            LocalTarget = localService,
        };

    public RpcRoutingInterceptor NewRoutingInterceptor(
        RpcServiceDef serviceDef, object? localTarget, object? remoteTarget)
        => new(RoutingInterceptorOptions, Hub.Services, serviceDef) {
            LocalTarget = localTarget,
            RemoteTarget = remoteTarget,
        };
}
