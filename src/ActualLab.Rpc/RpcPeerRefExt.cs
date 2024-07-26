using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

public static class RpcPeerRefExt
{
    public static string GetRemoteSideDescription(this RpcPeerRef peerRef)
        => (peerRef.IsBackend, peerRef.IsServer) switch {
            (true, true) => "backend client",
            (true, false) => "backend server",
            (false, true) => "client",
            (false, false) => "server",
        };

    public static TPeerRef RequireClient<TPeerRef>(this TPeerRef peerRef)
        where TPeerRef : RpcPeerRef
        => !peerRef.IsServer
            ? peerRef
            : throw Errors.ClientRpcPeerRefExpected(nameof(peerRef));

    public static TPeerRef RequireServer<TPeerRef>(this TPeerRef peerRef)
        where TPeerRef : RpcPeerRef
        => peerRef.IsServer
            ? peerRef
            : throw Errors.ServerRpcPeerRefExpected(nameof(peerRef));

    public static TPeerRef RequireBackend<TPeerRef>(this TPeerRef peerRef)
        where TPeerRef : RpcPeerRef
        => !peerRef.IsBackend
            ? peerRef
            : throw Errors.BackendRpcPeerRefExpected(nameof(peerRef));
}
