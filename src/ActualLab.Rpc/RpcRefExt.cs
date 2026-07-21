using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

/// <summary>
/// Extension methods for <see cref="RpcRef"/>.
/// </summary>
public static class RpcRefExt
{
    public static string GetRemotePartyName(this RpcRef rpcRef)
        => (rpcRef.IsBackend, rpcRef.IsServer) switch {
            (true, true) => "backend client",
            (true, false) => "backend server",
            (false, true) => "client",
            (false, false) => "server",
        };

    extension<TRef>(TRef rpcRef) where TRef : RpcRef
    {
        public TRef RequireClient()
            => !rpcRef.IsServer
                ? rpcRef
                : throw Errors.ClientRpcRefExpected(nameof(rpcRef));

        public TRef RequireServer()
            => rpcRef.IsServer
                ? rpcRef
                : throw Errors.ServerRpcRefExpected(nameof(rpcRef));

        public TRef RequireBackend()
            => rpcRef.IsBackend
                ? rpcRef
                : throw Errors.BackendRpcRefExpected(nameof(rpcRef));
    }
}
