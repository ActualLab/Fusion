namespace ActualLab.Fusion.Internal;

/// <summary>
/// Used by <see cref="ConsolidatingComputed{T}"/> to compare
/// <see cref="Computed.Output"/> values when consolidating
/// (i.e., when <see cref="ComputedOptions.ConsolidationDelay"/> is used).
/// </summary>
public delegate bool ComputedOutputEqualityComparer(Result x, Result y);
