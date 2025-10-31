using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Internal;

public sealed class RpcInternalServices(RpcHub hub) : IHasServices
{
    public RpcHub Hub = hub;
    public IServiceProvider Services { get; } = hub.Services;
    public RpcSerializationFormatResolver SerializationFormats => Hub.SerializationFormats;
    public RpcServiceDefBuilder ServiceDefBuilder => Hub.ServiceDefBuilder;
    public RpcMethodDefBuilder MethodDefBuilder => Hub.MethodDefBuilder;
    public RpcBackendServiceDetector BackendServiceDetector => Hub.BackendServiceDetector;
    public RpcCallTimeoutsProvider CallTimeoutsProvider => Hub.CallTimeoutsProvider;
    public RpcCallValidatorProvider CallValidatorProvider => Hub.CallValidatorProvider;
    public RpcServiceScopeResolver ServiceScopeResolver => Hub.ServiceScopeResolver;
    public RpcSafeCallRouter SafeCallRouter => Hub.SafeCallRouter;
    public RpcRerouteDelayer RerouteDelayer => Hub.RerouteDelayer;
    public RpcHashProvider HashProvider => Hub.HashProvider;
    public RpcInboundContextFactory InboundContextFactory => Hub.InboundContextFactory;
    public RpcInboundMiddlewares InboundMiddlewares => Hub.InboundMiddlewares;
    public RpcOutboundMiddlewares OutboundMiddlewares => Hub.OutboundMiddlewares;
    public RpcPeerFactory PeerFactory => Hub.PeerFactory;
    public RpcPeerConnectionKindResolver PeerConnectionKindResolver => Hub.PeerConnectionKindResolver;
    public RpcClientPeerReconnectDelayer ClientPeerReconnectDelayer => Hub.ClientPeerReconnectDelayer;
    public RpcServerPeerCloseTimeoutProvider ServerPeerCloseTimeoutProvider => Hub.ServerPeerCloseTimeoutProvider;
    public RpcPeerTerminalErrorDetector PeerTerminalErrorDetector => Hub.PeerTerminalErrorDetector;
    public RpcCallTracerFactory CallTracerFactory => Hub.CallTracerFactory;
    public RpcCallLoggerFactory CallLoggerFactory => Hub.CallLoggerFactory;
    public RpcCallLoggerFilter CallLoggerFilter => Hub.CallLoggerFilter;
    public IEnumerable<RpcPeerTracker> PeerTrackers => Hub.PeerTrackers;
    public RpcSystemCallSender SystemCallSender => Hub.SystemCallSender;
    public RpcClient Client => Hub.Client;
    public ConcurrentDictionary<RpcPeerRef, RpcPeer> Peers => Hub.Peers;

    public readonly RpcRoutingInterceptor.Options RoutingInterceptorOptions
        = hub.Services.GetRequiredService<RpcRoutingInterceptor.Options>();

    // NewXxx

    public IProxy NewRoutingProxy(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyBaseType,
        object? localTarget = null,
        bool initialize = true)
    {
        var interceptor = NewRoutingInterceptor(serviceType, localTarget);
        return Services.ActivateProxy(proxyBaseType, interceptor, initialize);
    }

    public RpcRoutingInterceptor NewRoutingInterceptor(
        Type serviceType,
        object? localTarget = null)
    {
        var serviceDef = Hub.ServiceRegistry[serviceType];
        return new RpcRoutingInterceptor(RoutingInterceptorOptions, Hub, serviceDef, localTarget);
    }
}
