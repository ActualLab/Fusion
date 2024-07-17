using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc.Internal;

public sealed class RpcInternalServices(RpcHub hub)
{
    private RpcInterceptor.Options? _interceptorOptions;
    private RpcSwitchInterceptor.Options? _switchInterceptorOptions;

    private RpcInterceptor.Options InterceptorOptions
        => _interceptorOptions ??= Hub.Services.GetRequiredService<RpcInterceptor.Options>();
    private RpcSwitchInterceptor.Options SwitchInterceptorOptions
        => _switchInterceptorOptions ??= Hub.Services.GetRequiredService<RpcSwitchInterceptor.Options>();

    public RpcHub Hub = hub;
    public RpcServiceDefBuilder ServiceDefBuilder => Hub.ServiceDefBuilder;
    public RpcMethodDefBuilder MethodDefBuilder => Hub.MethodDefBuilder;
    public RpcBackendServiceDetector BackendServiceDetector => Hub.BackendServiceDetector;
    public RpcCommandTypeDetector CommandTypeDetector => Hub.CommandTypeDetector;
    public RpcServiceScopeResolver ServiceScopeResolver => Hub.ServiceScopeResolver;
    public RpcSafeCallRouter CallRouter => Hub.CallRouter;
    public RpcRerouteDelayer RerouteDelayer => Hub.RerouteDelayer;
    public RpcArgumentSerializer ArgumentSerializer => Hub.ArgumentSerializer;
    public RpcInboundContextFactory InboundContextFactory => Hub.InboundContextFactory;
    public RpcInboundMiddlewares InboundMiddlewares => Hub.InboundMiddlewares;
    public RpcOutboundMiddlewares OutboundMiddlewares => Hub.OutboundMiddlewares;
    public RpcPeerFactory PeerFactory => Hub.PeerFactory;
    public RpcClientConnectionFactory ClientConnectionFactory => Hub.ClientConnectionFactory;
    public RpcClientPeerReconnectDelayer ClientPeerReconnectDelayer => Hub.ClientPeerReconnectDelayer;
    public RpcPeerTerminalErrorDetector PeerTerminalErrorDetector => Hub.PeerTerminalErrorDetector;
    public RpcMethodTracerFactory MethodTracerFactory => Hub.MethodTracerFactory;
    public RpcCallLoggerFactory CallLoggerFactory => Hub.CallLoggerFactory;
    public RpcCallLoggerFilter CallLoggerFilter => Hub.CallLoggerFilter;
    public IEnumerable<RpcPeerTracker> PeerTrackers => Hub.PeerTrackers;
    public RpcSystemCallSender SystemCallSender => Hub.SystemCallSender;
    public RpcClient Client => Hub.Client;

    public ConcurrentDictionary<RpcPeerRef, RpcPeer> Peers => Hub.Peers;

    public RpcInterceptor NewInterceptor(RpcServiceDef serviceDef, object? localTarget = null)
        => new(InterceptorOptions, Hub.Services, serviceDef) {
            LocalTarget = localTarget,
        };

    public RpcSwitchInterceptor NewSwitchInterceptor(
        RpcServiceDef serviceDef, object? localTarget, object? remoteTarget)
        => new(SwitchInterceptorOptions, Hub.Services, serviceDef) {
            LocalTarget = localTarget,
            RemoteTarget = remoteTarget,
        };
}
