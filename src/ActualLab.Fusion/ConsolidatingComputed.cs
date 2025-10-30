using ActualLab.Fusion.Interception;

namespace ActualLab.Fusion;

/// <summary>
/// A <see cref="Computed"/> that implements <see cref="ComputedOptions.IsConsolidating"/> behavior.
/// </summary>
/// <remarks>
/// It clones the <see cref="Original"/>'s <see cref="Computed.Output"/>,
/// invalidates <see cref="Original"/> on its own invalidation,
/// and invalidates itself after seeing the <see cref="Original"/>'s invalidation,
/// but only in case when a newly computed <see cref="Computed.Output"/> differs from its own one.
/// </remarks>
public interface IConsolidatingComputed : IComputed
{
    public Computed Original { get; }
}

/// <summary>
/// A <see cref="Computed"/> that implements <see cref="ComputedOptions.IsConsolidating"/> behavior.
/// </summary>
/// <typeparam name="T">The type of <see cref="Result"/>.</typeparam>
/// <remarks>
/// It clones the <see cref="Original"/>'s <see cref="Computed.Output"/>,
/// invalidates <see cref="Original"/> on its own invalidation,
/// and invalidates itself after seeing the <see cref="Original"/>'s invalidation,
/// but only in case when a newly computed <see cref="Computed.Output"/> differs from its own one.
/// </remarks>
public sealed class ConsolidatingComputed<T> : ComputeMethodComputed<T>, IConsolidatingComputed
{
    private volatile Computed<T> _original;

    Computed IConsolidatingComputed.Original => _original;
    public Computed<T> Original => _original;

    // ReSharper disable once ConvertToPrimaryConstructor
    public ConsolidatingComputed(ComputedOptions options, ComputeMethodInput input, Computed<T> original)
        : base(options, input, ((Computed)original).Output, original.IsConsistent())
    {
        _original = original;
        original.Invalidated += OnOriginalInvalidated;
    }

    protected override void OnInvalidated()
    {
        base.OnInvalidated();
        _original.Invalidate();
    }

    // Private methods

    private void OnOriginalInvalidated(Computed invalidated)
    {
        // ReSharper disable once InconsistentlySynchronizedField
        var original = _original;
        if (!(ReferenceEquals(invalidated, original) && this.IsConsistent()))
            return; // Concurrent update beat us

        _ = Task.Run(async () => {
            if (Options.ConsolidationDelay != TimeSpan.MaxValue)
                await Task.Delay(Options.ConsolidationDelay, CancellationToken.None).ConfigureAwait(false);
            if (!(ReferenceEquals(invalidated, original) && this.IsConsistent()))
                return; // Concurrent update beat us

            var nextOriginal = (Computed<T>)await original.UpdateUntyped(CancellationToken.None).ConfigureAwait(false);
            if (!ReferenceEquals(Interlocked.CompareExchange(ref _original, nextOriginal, original), original))
                return; // Concurrent update beat us

            if (nextOriginal.Output == Output)
                // original.Invalidated is auto-removed on invalidation
                nextOriginal.Invalidated += OnOriginalInvalidated;
            else
                Invalidate();
        }, CancellationToken.None);
    }
}
