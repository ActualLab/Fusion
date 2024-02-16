using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

public static class RpcPeerRefExt
{
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
