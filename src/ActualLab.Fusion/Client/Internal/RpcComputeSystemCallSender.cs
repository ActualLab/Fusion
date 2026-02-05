using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Internal;

/// <summary>
/// Sends Fusion compute system calls (such as invalidation notifications) to remote RPC peers.
/// </summary>
public sealed class RpcComputeSystemCallSender : RpcServiceBase
{
    public readonly RpcServiceDef ServiceDef;
    public readonly IRpcComputeSystemCalls Client;

    public RpcMethodDef InvalidateMethodDef
        => field ??= ServiceDef.Methods.Single(m => Equals(m.MethodInfo.Name, nameof(IRpcComputeSystemCalls.Invalidate)));

    public RpcComputeSystemCallSender(IServiceProvider services)
        : base(services)
    {
        ServiceDef = Hub.ServiceRegistry.Get<IRpcComputeSystemCalls>()!;
        Client = Hub.GetClient<IRpcComputeSystemCalls>();
    }

    public void Invalidate(RpcPeer peer, long callId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, callId, headers);
        var call = context.PrepareCallForSendNoWait(InvalidateMethodDef, ArgumentList.Empty)!;
        call.SendNoWait(needsPolymorphism: false);
    }
}
