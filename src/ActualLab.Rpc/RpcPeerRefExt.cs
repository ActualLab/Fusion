using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

public static class RpcPeerRefExt
{
    public static string GetRemotePartyName(this RpcPeerRef peerRef)
        => (peerRef.IsBackend, peerRef.IsServer) switch {
            (true, true) => "backend client",
            (true, false) => "backend server",
            (false, true) => "client",
            (false, false) => "server",
        };

    extension<TPeerRef>(TPeerRef peerRef) where TPeerRef : RpcPeerRef
    {
        public TPeerRef RequireClient()
            => !peerRef.IsServer
                ? peerRef
                : throw Errors.ClientRpcPeerRefExpected(nameof(peerRef));

        public TPeerRef RequireServer()
            => peerRef.IsServer
                ? peerRef
                : throw Errors.ServerRpcPeerRefExpected(nameof(peerRef));

        public TPeerRef RequireBackend()
            => !peerRef.IsBackend
                ? peerRef
                : throw Errors.BackendRpcPeerRefExpected(nameof(peerRef));
    }
}
