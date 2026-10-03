using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

public partial class RpcMethodDef
{
    public RpcCallTimeouts OutboundCallTimeouts { get; protected set; } = RpcCallTimeouts.None;
    public Func<ArgumentList, RpcRef>? OutboundCallRouter { get; protected set; } = null;
    public RpcLocalExecutionMode LocalExecutionMode { get; protected set; }
    public RpcRemoteExecutionMode RemoteExecutionMode { get; protected set; }

    // The delegates and properties below must be initialized in Initialize(),
    // they are supposed to be as efficient as possible (i.e., do less, if possible)
    // taking the values of other properties into account.
    public Func<RpcOutboundContext, RpcOutboundCall> OutboundCallFactory { get; protected set; } = null!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcOutboundCall? CreateOutboundCall(RpcOutboundContext context)
    {
        var peer = context.Peer;
        if (peer is null)
            throw ActualLab.Internal.Errors.InternalError("context.Peer is null.");

        if (peer.ConnectionKind is RpcPeerConnectionKind.Local)
            return null;

        return context.CallTypeId is { } callTypeId && callTypeId != CallType.Id
            ? RpcOutboundCall.GetFactory(this, callTypeId).Invoke(context)
            : OutboundCallFactory.Invoke(context);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcPeer RouteCall(ArgumentList args, RpcRoutingMode routingMode)
        => routingMode switch {
            RpcRoutingMode.Outbound => RouteOutboundCall(args),
            RpcRoutingMode.Inbound => RouteInboundCall(args),
            RpcRoutingMode.Prerouted => Hub.LocalPeer, // This overload assumes the peer is local in this case!
            _ => throw new ArgumentOutOfRangeException(nameof(routingMode), routingMode, null),
        };


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcPeer RouteCall(ArgumentList args, RpcRoutingMode routingMode, RpcPeer? preroutedPeer)
        => routingMode switch {
            RpcRoutingMode.Outbound => RouteOutboundCall(args),
            RpcRoutingMode.Inbound => RouteInboundCall(args),
            RpcRoutingMode.Prerouted => preroutedPeer ?? throw new ArgumentNullException(nameof(preroutedPeer)),
            _ => throw new ArgumentOutOfRangeException(nameof(routingMode), routingMode, null),
        };

    public RpcPeer RouteOutboundCall(ArgumentList args)
    {
        while (true) {
            try {
                return Hub.GetPeer(GetOutboundCallRoute(args));
            }
            catch (RpcRerouteException e) {
                // This should never happen, but just in case...
                Log.LogWarning(e, "Rerouted while routing: {Method}{Arguments}", this, args);
            }
        }
    }

    public RpcRoute GetOutboundCallRoute(ArgumentList args)
    {
        if (IsSystem)
            throw Errors.SystemCallsMustBePrerouted();

        return OutboundCallRouter!.Invoke(args).Route;
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

    // An ownership test, so it stops at the route: RouteOutboundCall would hand that route to
    // Hub.GetPeer, which mints a peer for a host we have no intention of calling. The connection
    // kind comes from the route either way - this is the derivation RpcPeer's constructor uses.
    // RpcRerouteException is deliberately not caught: the only caller is an inbound invalidation,
    // where that exception is the answer rather than a failure - the same one RouteInboundCall
    // throws for a call that isn't ours. Retrying it here would spin while the mesh rebalances.
    public bool IsLocalCall(ArgumentList args)
    {
        if (Service.Mode is not RpcServiceMode.Distributed)
            return true;

        var route = GetOutboundCallRoute(args);
        return route.GetConnectionKind(Hub.PeerOptions) is RpcPeerConnectionKind.Local;
    }

    // Protected methods

    protected internal virtual RpcDelayedCallAction GetDefaultDelayedCallAction()
        => CallType.Id == RpcCallTypeIds.Compute
            ? RpcDelayedCallAction.LogAndResend
            : RpcDelayedCallAction.Log;

    protected virtual RpcLocalExecutionMode GetDefaultLocalExecutionMode()
    {
        if (Service.Mode is not RpcServiceMode.Distributed)
            return RpcLocalExecutionMode.Unconstrained;

        return CallType.Id == RpcCallTypeIds.Compute
            ? RpcLocalExecutionMode.ConstrainedEntry
            : RpcLocalExecutionMode.Constrained;
    }
}
