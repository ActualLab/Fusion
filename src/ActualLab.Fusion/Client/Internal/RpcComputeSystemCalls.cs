using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Internal;

public interface IRpcComputeSystemCalls : IRpcSystemService
{
    public Task<RpcNoWait> Invalidate();
}

public class RpcComputeSystemCalls(IServiceProvider services)
    : RpcServiceBase(services), IRpcComputeSystemCalls
{
    public static readonly string Name = "$sys-c";

    public Task<RpcNoWait> Invalidate()
    {
        var context = RpcInboundContext.GetCurrent();
        var peer = context.Peer;
        var outboundCallId = context.Message.RelatedId;
        if (peer.OutboundCalls.Get(outboundCallId) is RpcOutboundComputeCall outboundCall)
            outboundCall.SetInvalidated(context, "RPC");
        return RpcNoWait.Tasks.Completed;
    }
}
