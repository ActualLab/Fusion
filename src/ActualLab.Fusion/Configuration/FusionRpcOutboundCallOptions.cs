using ActualLab.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion;

public class FusionRpcOutboundCallOptions : RpcOutboundCallOptions
{
    public static new FusionRpcOutboundCallOptions Default { get; set; } = new();

    public override Func<ArgumentList, RpcPeerRef> CreateRouter(RpcMethodDef methodDef)
        => methodDef.Kind is RpcMethodKind.Command
            ? static args => Invalidation.IsActive ? RpcPeerRef.Local : RpcPeerRef.Default
            : static args => RpcPeerRef.Default;
}
