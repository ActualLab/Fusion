using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

/// <summary>
/// A <see cref="Computed"/> that implements <see cref="ComputedOptions.IsConsolidating"/> behavior.
/// </summary>
/// <remarks>
/// It clones the <see cref="Source"/>'s <see cref="Computed.Output"/>
/// and invalidates itself after seeing the <see cref="Source"/>'s invalidation,
/// but only in case when a newly computed <see cref="Computed.Output"/> differs from its own one.
/// </remarks>
public interface IConsolidatingComputed : IInvalidationProxyComputed
{
    public Computed Source { get; }
    public Task? WhenConsolidated { get; }
}

/// <summary>
/// A <see cref="Computed"/> that implements <see cref="ComputedOptions.IsConsolidating"/> behavior.
/// </summary>
/// <typeparam name="T">The type of <see cref="Result"/>.</typeparam>
/// <remarks>
/// It clones the <see cref="Source"/>'s <see cref="Computed.Output"/>
/// and invalidates itself after seeing the <see cref="Source"/>'s invalidation,
/// but only in case when a newly computed <see cref="Computed.Output"/> differs from its own one.
/// </remarks>
public sealed class ConsolidatingComputed<T> : ComputeMethodComputed<T>, IConsolidatingComputed
{
    private volatile Computed<T> _source;
    private volatile Task? _whenConsolidated;

    Computed IConsolidatingComputed.Source => _source;
    public Computed<T> Source => _source;
    public Task? WhenConsolidated => _whenConsolidated;

    // IInvalidationProxyComputed
    Computed IInvalidationProxyComputed.InvalidationTarget => _source;

    // ReSharper disable once ConvertToPrimaryConstructor
    public ConsolidatingComputed(ComputedOptions options, ComputeMethodInput input, Computed<T> source)
        : base(options, input, source.UntypedOutput, source.IsConsistent())
    {
        _source = source;
        source.Invalidated += OnSourceInvalidated;
    }

    // Private methods

    private void OnSourceInvalidated(Computed invalidated)
    {
        if (_whenConsolidated is not null) return; // Double-check locking
        lock (Lock) {
            if (_whenConsolidated is not null) return;

            _whenConsolidated = this.IsConsistent()
                ? Task.Run(Consolidate, CancellationToken.None)
                : Task.CompletedTask; // No need to consolidate: we're to be replaced anyway
        }
        // _source.Invalidated is auto-removed on invalidation, so we don't need to bother about this
        return;

        async Task Consolidate() {
            Computed<T>? nextSource = null; // null means to Invalidate(), which is the default if this method fails
            try {
                if (Options.ConsolidationDelay != TimeSpan.MaxValue)
                    await Task.Delay(Options.ConsolidationDelay, CancellationToken.None).ConfigureAwait(false);

                var updatedSource = (Computed<T>)await _source.UpdateUntyped(CancellationToken.None).ConfigureAwait(false);
                var outputEqualityComparer = Input.Function.Hub.ComputedOutputEqualityComparer;
                nextSource = outputEqualityComparer.Invoke(UntypedOutput, updatedSource.UntypedOutput)
                    ? updatedSource
                    : null; // Invalidate
            }
            finally {
                if (nextSource is null) {
                    // No next source -> we're invalidating ourselves
                    Invalidate(immediately: true);
                }
                else {
                    // 1. Re-enable the consolidation
                    lock (Lock) {
                        _source = nextSource;
                        _whenConsolidated = null;
                    }
                    // 2. Subscribe to the next source's invalidation'
                    nextSource.Invalidated += OnSourceInvalidated;
                }
            }
        }

    }
}
