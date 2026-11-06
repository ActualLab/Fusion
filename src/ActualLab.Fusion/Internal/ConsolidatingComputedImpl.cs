namespace ActualLab.Fusion.Internal;

/// <summary>
/// Non-generic state shared by all <see cref="ConsolidatingComputed{T}"/> types.
/// </summary>
public static class ConsolidatingComputedImpl
{
    // The ConsolidatingComputed this thread invalidates as the outcome of its consolidation
    [field: ThreadStatic]
    public static IConsolidatingComputed? InvalidatingConsolidationTarget { get; set; }
}
