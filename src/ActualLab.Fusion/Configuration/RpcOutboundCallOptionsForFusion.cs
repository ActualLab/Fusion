using ActualLab.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion;

public record RpcOutboundCallOptionsForFusion : RpcOutboundCallOptions
{
    public static new RpcOutboundCallOptionsForFusion Default { get; set; } = new();

    public RpcOutboundCallOptionsForFusion()
        => RouterFactory = DefaultRouterFactory;

    // Protected methods

    protected static new Func<ArgumentList, RpcPeerRef> DefaultRouterFactory(RpcMethodDef methodDef)
        => methodDef.Kind is RpcMethodKind.Command
            ? static args => Invalidation.IsActive ? RpcPeerRef.Local : RpcPeerRef.Default
            : static args => RpcPeerRef.Default;
}
