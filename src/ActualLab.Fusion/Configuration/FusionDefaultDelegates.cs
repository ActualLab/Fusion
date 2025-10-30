using ActualLab.Fusion.Internal;
using ActualLab.Rpc;

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

                return x.Error.GetType() == y.Error.GetType()
                    && string.Equals(x.Error.Message, y.Error.Message, StringComparison.Ordinal);
            }

            return y.Error is null && Equals(x.Value, y.Value);
        };

    /// <summary>
    /// Used by <c>.AddFusion</c> method to replace the default <see cref="RpcCallRouter"/>.
    /// This call router ensures that any command method call is routed to
    /// <see cref="RpcPeerRef.Local"/> when <see cref="Invalidation.IsActive"/>,
    /// i.e., no commands are sent to remote peers while invalidation is active.
    /// </summary>
    public static RpcCallRouter CallRouter { get; set; }
        = static (method, arguments) => method.IsCommand && Invalidation.IsActive
            ? RpcPeerRef.Local
            : RpcPeerRef.Default;
}
