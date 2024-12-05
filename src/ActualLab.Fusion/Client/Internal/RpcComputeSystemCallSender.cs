using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Internal;

public sealed class RpcComputeSystemCallSender(IServiceProvider services)
    : RpcServiceBase(services)
{
    [field: AllowNull, MaybeNull]
    private IRpcComputeSystemCalls Client => field
        ??= Services.GetRequiredService<IRpcComputeSystemCalls>();
    [field: AllowNull, MaybeNull]
    private RpcServiceDef ComputeSystemCallsServiceDef => field
        ??= Hub.ServiceRegistry.Get<IRpcComputeSystemCalls>()!;
    [field: AllowNull, MaybeNull]
    private RpcMethodDef InvalidateMethodDef => field
        ??= ComputeSystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcComputeSystemCalls.Invalidate)));

    public Task Invalidate(RpcPeer peer, long callId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, callId, headers);
        var call = context.PrepareCallForSendNoWait(InvalidateMethodDef, ArgumentList.Empty)!;
        return call.SendNoWait(false);
    }
}
