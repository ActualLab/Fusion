using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

#pragma warning disable CA1813 // Consider making sealed

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class ComputeMethodAttribute : Attribute
{
    /// <summary>
    /// Minimum time (in seconds) for any produced <see cref="Computed"/> instance to stay in RAM.
    /// <code>double.NaN</code> means "use default", which is <see cref="TimeSpan.Zero"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In fact, it's a duration of a period during which a strong reference to this
    /// <see cref="Computed"/> instance is maintained via <see cref="Timeouts"/>.
    /// </para>
    /// <para>
    /// Invalidation trims this time to the invalidation point - there is no reason
    /// to cache an outdated computed; moreover, there is no way to get it
    /// (unless you already have a strong reference to it).
    /// </para>
    /// </remarks>
    public double MinCacheDuration { get; set; } = double.NaN;

    /// <summary>
    /// Auto-invalidation delay (in seconds) for any produced <see cref="Computed"/> instance
    /// which stores an error, and it's a transient error.
    /// <code>double.NaN</code> means "use default", which is 1 second.
    /// </summary>
    public double TransientErrorInvalidationDelay { get; set; } = double.NaN;

    /// <summary>
    /// Auto-invalidation delay (in seconds) for any produced <see cref="Computed"/> instance.
    /// <code>double.NaN</code> means "use default", which is <see cref="TimeSpan.MaxValue"/> (no auto invalidation).
    /// </summary>
    public double AutoInvalidationDelay { get; set; } = double.NaN;

    /// <summary>
    /// Invalidation delay (in seconds) for any produced <see cref="Computed"/> instance.
    /// <code>double.NaN</code> means "use default", which is <see cref="TimeSpan.Zero"/> (no invalidation delay).
    /// </summary>
    public double InvalidationDelay { get; set; } = double.NaN;

    /// <summary>
    /// Consolidation delay (in seconds) for any produced <see cref="Computed"/> instance.
    /// <code>double.NaN</code> means "use default", which is <see cref="TimeSpan.MaxValue"/> (no consolidation).
    /// </summary>
    public double ConsolidationDelay { get; set; } = double.NaN;
}
