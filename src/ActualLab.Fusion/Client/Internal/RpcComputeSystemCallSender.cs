using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Internal;

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

    public Task Invalidate(RpcPeer peer, long callId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, callId, headers);
        var call = context.PrepareCallForSendNoWait(InvalidateMethodDef, ArgumentList.Empty)!;
        return call.SendNoWaitSilently(needsPolymorphism: false);
    }
}
