using ActualLab.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Rpc;

public static class RpcOutboundCallOptionsExt
{
    extension(RpcOutboundCallOptions options)
    {
        public RpcOutboundCallOptions WithFusionOverrides()
            => options with { RouterFactory = RouterFactory };
    }

    // Private methods

    private static Func<ArgumentList, RpcPeerRef> RouterFactory(RpcMethodDef methodDef)
        => methodDef.Kind is RpcMethodKind.Command
            ? static args => Invalidation.IsActive ? RpcPeerRef.Local : RpcPeerRef.Default
            : static args => RpcPeerRef.Default;
}
