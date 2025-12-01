using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Internal;

public sealed class RpcInternalServices(RpcHub hub) : IHasServices
{
    public RpcHub Hub = hub;
    public IServiceProvider Services { get; } = hub.Services;
    public RpcRegistryOptions RegistryOptions => Hub.RegistryOptions;
    public RpcPeerOptions PeerOptions => Hub.PeerOptions;
    public RpcInboundCallOptions InboundCallOptions => Hub.InboundCallOptions;
    public RpcOutboundCallOptions OutboundCallOptions => Hub.OutboundCallOptions;
    public RpcDiagnosticsOptions DiagnosticsOptions => Hub.DiagnosticsOptions;
    public IRpcMiddleware[] Middlewares => Hub.Middlewares;
    public RpcClientPeerReconnectDelayer ClientPeerReconnectDelayer => Hub.ClientPeerReconnectDelayer;
    public RpcSystemCallSender SystemCallSender => Hub.SystemCallSender;
    public RpcClient Client => Hub.Client;
    public ConcurrentDictionary<RpcPeerRef, RpcPeer> Peers => Hub.Peers;

    public readonly RpcInterceptor.Options InterceptorOptions
        = hub.Services.GetRequiredService<RpcInterceptor.Options>();

    // NewXxx

    public IProxy NewProxy(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyBaseType,
        object? localTarget = null,
        bool initialize = true)
    {
        var interceptor = NewInterceptor(serviceType, localTarget);
        return Services.ActivateProxy(proxyBaseType, interceptor, initialize);
    }

    public RpcInterceptor NewInterceptor(Type serviceType, object? localTarget = null)
    {
        var serviceDef = Hub.ServiceRegistry[serviceType];
        return new RpcInterceptor(InterceptorOptions, Hub, serviceDef, localTarget);
    }
}
