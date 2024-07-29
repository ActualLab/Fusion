using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Internal;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Internal;

public sealed class RpcComputeSystemCallSender(IServiceProvider services)
    : RpcServiceBase(services)
{
    private IRpcComputeSystemCalls? _client;
    private RpcServiceDef? _computeSystemCallsServiceDef;
    private RpcMethodDef? _invalidateMethodDef;

    private IRpcComputeSystemCalls Client => _client
        ??= Services.GetRequiredService<IRpcComputeSystemCalls>();
    private RpcServiceDef ComputeSystemCallsServiceDef => _computeSystemCallsServiceDef
        ??= Hub.ServiceRegistry.Get<IRpcComputeSystemCalls>()!;
    private RpcMethodDef InvalidateMethodDef => _invalidateMethodDef
        ??= ComputeSystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcComputeSystemCalls.Invalidate)));

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Invalidate(RpcPeer peer, long callId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, callId, headers);
        var call = context.PrepareNoWaitCall(InvalidateMethodDef, ArgumentList.Empty)!;
        return call.SendNoWait(false);
    }
}
