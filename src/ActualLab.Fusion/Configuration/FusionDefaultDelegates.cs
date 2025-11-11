using ActualLab.Fusion.Internal;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion;

public static class FusionDefaultDelegates
{
    /// <summary>
    /// Used by <see cref="ConsolidatingComputed{T}"/> to compare
    /// <see cref="Computed.Output"/> values when consolidating
    /// (i.e., when <see cref="ComputedOptions.ConsolidationDelay"/> is used).
    /// </summary>
    public static ComputedOutputEqualityComparer ComputedOutputEqualityComparer { get; set; }
        = (x, y) => {
            if (x.Error is not null) {
                if (y.Error is null)
                    return false;

                // Errors are identical when they have the same type and message
                return x.Error.GetType() == y.Error.GetType()
                    && string.Equals(x.Error.Message, y.Error.Message, StringComparison.Ordinal);
            }

            return y.Error is null && Equals(x.Value, y.Value);
        };

    /// <summary>
    /// Used by <c>.AddFusion</c> method to replace the default <see cref="RpcOutboundCallHandlerFactory"/>.
    /// This call router ensures that any command method call is routed to
    /// <see cref="RpcPeerRef.Local"/> when <see cref="Invalidation.IsActive"/>,
    /// i.e., no commands are sent to remote peers while invalidation is active.
    /// </summary>
    public static 0RpcOutboundCallHandlerFactory OutboundCallHandlerFactory { get; set; }
        = static method => new RpcOutboundCallHandler(method) {
            Router = method.Kind is RpcMethodKind.Command
                ? static args => Invalidation.IsActive ? RpcPeerRef.Local : RpcPeerRef.Default
                : static args => RpcPeerRef.Default,
        };
}
