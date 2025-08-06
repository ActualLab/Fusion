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

    public readonly RpcInterceptorOptions InterceptorOptions
        = hub.Services.GetRequiredService<RpcInterceptorOptions>();

    // NewXxx

    public IProxy NewNonRoutingProxy(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyBaseType,
        object? localTarget = null,
        bool initialize = true)
    {
        var interceptor = NewNonRoutingInterceptor(serviceType, localTarget);
        return Services.ActivateProxy(proxyBaseType, interceptor, null, initialize);
    }

    public RpcNonRoutingInterceptor NewNonRoutingInterceptor(Type serviceType, object? localTarget = null, bool assumeConnected = false)
    {
        var serviceDef = Hub.ServiceRegistry[serviceType];
        return new RpcNonRoutingInterceptor(InterceptorOptions, Hub.Services, serviceDef, localTarget, assumeConnected);
    }

    public IProxy NewRoutingProxy(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyBaseType,
        object? localTarget = null,
        bool initialize = true)
    {
        var interceptor = NewRoutingInterceptor(serviceType, localTarget);
        return Services.ActivateProxy(proxyBaseType, interceptor, proxyTarget: null, initialize);
    }

    public RpcRoutingInterceptor NewRoutingInterceptor(
        Type serviceType,
        object? localTarget = null,
        bool assumeConnected = false)
    {
        var serviceDef = Hub.ServiceRegistry[serviceType];
        return new RpcRoutingInterceptor(InterceptorOptions, Hub.Services, serviceDef, localTarget, assumeConnected);
    }

    public IProxy NewSwitchProxy(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyBaseType,
        object? localTarget,
        object? remoteTarget,
        bool initialize = true)
    {
        var interceptor = NewSwitchInterceptor(serviceType, localTarget, remoteTarget);
        return Services.ActivateProxy(proxyBaseType, interceptor, null, initialize);
    }

    public RpcSwitchInterceptor NewSwitchInterceptor(Type serviceType, object? localTarget, object? remoteTarget)
    {
        var serviceDef = Hub.ServiceRegistry[serviceType];
        return new RpcSwitchInterceptor(InterceptorOptions, Hub.Services, serviceDef, localTarget, remoteTarget);
    }
}
