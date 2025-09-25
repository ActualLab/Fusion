using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

public static class RpcCallRouteOverride
{
    [field: ThreadStatic]
    public static RpcPeer? Peer { get; internal set; }

    public static Scope Activate(RpcPeer peer)
        => new(peer);

    public static RpcPeer? ApplyAndReset(RpcOutboundContext? context)
    {
        var peer = Peer;
        if (peer is not null)
            context?.Peer = peer;
        else
            peer = context?.Peer;
        Peer = null;
        return peer;
    }

    // Nested types

    public readonly struct Scope : IDisposable
    {
        private readonly RpcPeer? _oldPeer;

        // ReSharper disable once MemberHidesStaticFromOuterClass
        public readonly RpcPeer Peer;

        internal Scope(RpcPeer peer)
            : this(peer, RpcCallRouteOverride.Peer)
        { }

        internal Scope(RpcPeer peer, RpcPeer? oldPeer)
        {
            Peer = peer;
            _oldPeer = oldPeer;
            if (!ReferenceEquals(Peer, _oldPeer))
                RpcCallRouteOverride.Peer = Peer;
        }

        public void Dispose()
        {
            if (ReferenceEquals(Peer, _oldPeer))
                return; // Default or no-op instance

            var peer = RpcCallRouteOverride.Peer;
            if (peer is not null && !ReferenceEquals(Peer, peer))
                throw Errors.RpcCallRouteOverrideChanged();

            RpcCallRouteOverride.Peer = _oldPeer;
        }
    }

}
