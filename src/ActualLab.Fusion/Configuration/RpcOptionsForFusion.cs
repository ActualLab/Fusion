using ActualLab.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion;

public static class RpcOptionsForFusion
{
    public static RpcOutboundCallOptions DefaultOutboundCallOptions { get; set; }
        = new() { RouterFactory = FusionRouterFactory };

    // Private methods

    private static Func<ArgumentList, RpcPeerRef> FusionRouterFactory(RpcMethodDef methodDef)
        => methodDef.Kind is RpcMethodKind.Command
            ? static args => Invalidation.IsActive ? RpcPeerRef.Local : RpcPeerRef.Default
            : static args => RpcPeerRef.Default;
}
