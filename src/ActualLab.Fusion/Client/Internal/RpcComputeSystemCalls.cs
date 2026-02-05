using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Internal;

/// <summary>
/// Defines the RPC system service contract for Fusion compute invalidation notifications.
/// </summary>
public interface IRpcComputeSystemCalls : IRpcSystemService
{
    public Task<RpcNoWait> Invalidate();
}

/// <summary>
/// Implements <see cref="IRpcComputeSystemCalls"/> to handle incoming invalidation messages
/// from remote peers.
/// </summary>
public class RpcComputeSystemCalls(IServiceProvider services)
    : RpcServiceBase(services), IRpcComputeSystemCalls
{
    public static readonly string Name = "$sys-c";

    public Task<RpcNoWait> Invalidate()
    {
        var context = RpcInboundContext.GetCurrent();
        var peer = context.Peer;
        var outboundCallId = context.Message.RelatedId;
        if (peer.OutboundCalls.Get(outboundCallId) is RpcOutboundComputeCall outboundCall) {
            const string reason =
                $"<FusionRpc>.{nameof(Invalidate)}";
            outboundCall.SetInvalidated(context, reason);
        }
        return RpcNoWait.Tasks.Completed;
    }
}
