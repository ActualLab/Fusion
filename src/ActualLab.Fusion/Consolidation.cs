using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

/// <summary>
/// Provides <see cref="Bypass"/>, which makes the calls of consolidating compute methods
/// use their consolidation sources, i.e. act as if there is no consolidation.
/// </summary>
public static class Consolidation
{
    // Adds BypassConsolidation to the current context: inside a compute method the calls still become
    // its dependencies, and inside an invalidation block they invalidate only the consolidation sources.
    public static ComputeContextScope Bypass(bool isolate = false)
    {
        var oldContext = ComputeContext.Current;
        var callOptions = oldContext.CallOptions;
        // The computed captured in the new context wouldn't reach the current one:
        // use Computed.Capture(bypassConsolidation: true, ...) instead
        if (callOptions.HasFlag(CallOptions.Capture))
            throw Errors.InvalidContextCallOptions(callOptions);
        // A captured invalidation is replayed without the flag, i.e. it invalidates the targets too
        if (callOptions.HasFlag(CallOptions.CaptureInvalidation))
            return new(oldContext);

        var context = new ComputeContext(
            isolate ? null : oldContext.Computed,
            callOptions | CallOptions.BypassConsolidation,
            oldContext.InvalidationSource);
        return new(context);
    }
}
