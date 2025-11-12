using ActualLab.Interception;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

public partial class RpcMethodDef
{
    public RpcCallTimeoutSet OutboundCallTimeouts { get; init; } = RpcCallTimeoutSet.None;
    public Func<ArgumentList, RpcPeerRef>? OutboundCallRouter { get; init; } = null;

    public RpcPeer RouteOutboundCall(ArgumentList args)
    {
        if (IsSystem)
            throw Errors.SystemCallsMustBePrerouted();

        while (true) {
            try {
                var peerRef = OutboundCallRouter!.Invoke(args);
                return Hub.GetPeer(peerRef);
            }
            catch (RpcRerouteException e) {
                // This should never happen, but just in case...
                Log.LogWarning(e, "Rerouted while routing: {Method}{Arguments}", this, args);
            }
        }
    }
}
