using ActualLab.Rpc;

namespace ActualLab.Fusion;

public static class FusionRpcDefaultDelegates
{
    public static RpcCallRouter CallRouter { get; set; }
        = static (method, arguments) => method.IsCommand && Invalidation.IsActive
            ? RpcPeerRef.Local
            : RpcPeerRef.Default;
}
