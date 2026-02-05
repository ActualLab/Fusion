using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

/// <summary>
/// Provides default delegate instances used by Fusion infrastructure.
/// </summary>
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
}
