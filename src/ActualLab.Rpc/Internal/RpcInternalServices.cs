using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc;
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
    public RpcWebSocketClientOptions WebSocketClientOptions => Hub.WebSocketClientOptions;
    public RpcDiagnosticsOptions DiagnosticsOptions => Hub.DiagnosticsOptions;
    public RpcClientPeerReconnectDelayer ClientPeerReconnectDelayer => Hub.ClientPeerReconnectDelayer;
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
