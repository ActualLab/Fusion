using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Internal;

public interface IRpcComputeSystemCalls : IRpcSystemService
{
    [RequiresUnreferencedCode(Stl.Internal.UnreferencedCode.Serialization)]
    Task<RpcNoWait> Invalidate();
}

public class RpcComputeSystemCalls(IServiceProvider services)
    : RpcServiceBase(services), IRpcComputeSystemCalls
{
    public static readonly Symbol Name = "$sys-c";

    [RequiresUnreferencedCode(Stl.Internal.UnreferencedCode.Serialization)]
    public Task<RpcNoWait> Invalidate()
    {
        var context = RpcInboundContext.GetCurrent();
        var peer = context.Peer;
        var outboundCallId = context.Message.RelatedId;
        if (peer.OutboundCalls.Get(outboundCallId) is IRpcOutboundComputeCall outboundCall)
            outboundCall.SetInvalidated(context);
        return RpcNoWait.Tasks.Completed;
    }
}
