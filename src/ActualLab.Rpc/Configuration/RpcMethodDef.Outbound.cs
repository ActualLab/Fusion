using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

public partial class RpcMethodDef
{
    public RpcCallTimeouts OutboundCallTimeouts { get; protected set; } = RpcCallTimeouts.None;
    public Func<ArgumentList, RpcPeerRef>? OutboundCallRouter { get; protected set; } = null;
    public RpcLocalExecutionMode LocalExecutionMode { get; protected set; }

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

    public RpcPeer RouteCall(ArgumentList args, RpcRoutingMode routingMode)
        => routingMode switch {
            RpcRoutingMode.Outbound => RouteOutboundCall(args),
            RpcRoutingMode.Inbound => RouteInboundCall(args),
            RpcRoutingMode.Prerouted => Hub.LocalPeer, // This overload assumes the peer is local in this case!
            _ => throw new ArgumentOutOfRangeException(nameof(routingMode), routingMode, null),
        };


    public RpcPeer RouteCall(ArgumentList args, RpcRoutingMode routingMode, RpcPeer? preroutedPeer)
        => routingMode switch {
            RpcRoutingMode.Outbound => RouteOutboundCall(args),
            RpcRoutingMode.Inbound => RouteInboundCall(args),
            RpcRoutingMode.Prerouted => preroutedPeer ?? throw new ArgumentNullException(nameof(preroutedPeer)),
            _ => throw new ArgumentOutOfRangeException(nameof(routingMode), routingMode, null),
        };

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

    public RpcPeer RouteInboundCall(ArgumentList args)
    {
        if (Service.Mode is not RpcServiceMode.Distributed)
            return Hub.LocalPeer;

        var peer = RouteOutboundCall(args);
        if (peer.ConnectionKind is RpcPeerConnectionKind.Local)
            return peer;

        // Inbound RPC calls to distributed services must be routed to local peers only
        throw RpcRerouteException.MustRerouteInbound();
    }
}
