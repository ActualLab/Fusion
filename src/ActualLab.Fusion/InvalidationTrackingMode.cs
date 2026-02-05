namespace ActualLab.Fusion;

/// <summary>
/// Defines how much detail is retained when tracking invalidation sources.
/// </summary>
public enum InvalidationTrackingMode
{
    /// <summary>
    /// It's the "fastest" mode, but it doesn't really track invalidation sources.
    /// <see cref="InvalidationSource.ForCurrentLocation"/> always returns
    /// <see cref="InvalidationSource.Unknown"/> invalidation source.
    /// <see cref="Computed.InvalidationSource"/> may store only string-based invalidation sources,
    /// it is either <see cref="InvalidationSource.None"/>, <see cref="InvalidationSource.Unknown"/>,
    /// or equal to one of pre-defined sources in this mode.
    /// Dependencies copy the invalidation source of their parent.
    /// </summary>
    None = 0,
    /// <summary>
    /// This is the default invalidation tracking mode.
    /// <see cref="InvalidationSource.ForCurrentLocation"/> returns location-based invalidation source.
    /// <see cref="Computed.InvalidationSource"/> stores only string-based invalidation sources.
    /// Dependencies copy the invalidation source of their parent,
    /// so you can find out the origin of invalidation for any invalidated computed,
    /// but not the whole invalidation chain.
    /// </summary>
    OriginOnly,
    /// <summary>
    /// <see cref="InvalidationSource.ForCurrentLocation"/> returns location-based invalidation source.
    /// <see cref="Computed.InvalidationSource"/> stores both string-based invalidation sources
    /// and computed instances.
    /// Dependencies store their parent as their invalidation source,
    /// so you can walk through the whole invalidation chain.
    /// This is the most comprehensive invalidation tracking mode.
    /// Its main downside is increased memory use: every inconsistent <see cref="Computed"/> instance
    /// referenced by your code references its invalidation chain, i.e., typically 3-5 more instances.
    /// It's recommended to turn this mode on mainly to analyze complex invalidation issues.
    /// </summary>
    WholeChain,
}
