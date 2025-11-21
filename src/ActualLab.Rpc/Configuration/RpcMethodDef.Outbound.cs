using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

public partial class RpcMethodDef
{
    public RpcCallTimeouts OutboundCallTimeouts { get; protected set; } = RpcCallTimeouts.None;
    public Func<ArgumentList, RpcPeerRef>? OutboundCallRouter { get; protected set; } = null;
    public RpcLocalExecutionMode OutboundCallLocalExecutionMode { get; protected set; } = RpcLocalExecutionMode.Unconstrained;

    // The delegates and properties below must be initialized in Initialize(),
    // they are supposed to be as efficient as possible (i.e., do less, if possible)
    // taking the values of other properties into account.
    public Func<RpcOutboundContext, RpcOutboundCall> OutboundCallFactory { get; protected set; } = null!;

    public RpcOutboundCall? CreateOutboundCall(RpcOutboundContext context)
    {
        var peer = context.Peer;
        if (peer is null)
            throw ActualLab.Internal.Errors.InternalError("context.Peer is null.");

        return peer.ConnectionKind is RpcPeerConnectionKind.Local
            ? null
            : OutboundCallFactory.Invoke(context);
    }

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
