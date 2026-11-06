using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using static ActualLab.Fusion.Internal.ConsolidatingComputedImpl;

namespace ActualLab.Fusion;

/// <summary>
/// A <see cref="Computed"/> that implements <see cref="ComputedOptions.IsConsolidating"/> behavior.
/// </summary>
/// <remarks>
/// It clones the <see cref="Source"/>'s <see cref="Computed.Output"/>
/// and invalidates itself after seeing the <see cref="Source"/>'s invalidation,
/// but only in case when a newly computed <see cref="Computed.Output"/> differs from its own one.
/// Any other invalidation of it invalidates the <see cref="Source"/> too.
/// </remarks>
public interface IConsolidatingComputed : IHasInvalidationTarget, IHasSynchronizationTarget
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
/// Any other invalidation of it invalidates the <see cref="Source"/> too.
/// </remarks>
public sealed class ConsolidatingComputed<T> : ComputeMethodComputed<T>, IConsolidatingComputed
{
    private Computed<T> _source;
    private Task? _whenConsolidated;

    Computed IConsolidatingComputed.Source => _source;
    public Computed<T> Source => _source;
    // ReSharper disable once InconsistentlySynchronizedField
    public Task? WhenConsolidated => Volatile.Read(ref _whenConsolidated);

    // IHasInvalidationTarget
    Computed? IHasInvalidationTarget.InvalidationTarget => _source;
    // IHasSynchronizationTarget
    Computed? IHasSynchronizationTarget.SynchronizationTarget => _source;

    // ReSharper disable once ConvertToPrimaryConstructor
    public ConsolidatingComputed(ComputedOptions options, ComputeMethodInput input, Computed<T> source)
        : base(options, input, source.UntypedOutput, source.IsConsistent())
    {
        _source = source;
        source.Invalidated += OnSourceInvalidated;
    }

    // Protected methods

    protected override void OnInvalidated()
    {
        base.OnInvalidated();
        // Consolidation invalidates us only after computing a new source with a different output,
        // and the next call must reuse that source rather than recompute it. Thread-static, so a direct
        // Invalidate() on another thread winning the race with consolidation still invalidates the source.
        if (ReferenceEquals(InvalidatingConsolidationTarget, this))
            return;

        // Otherwise the next call would create a new consolidating computed over the same source.
        // It's the registered source that gets invalidated: a running consolidation may be computing
        // a newer one than _source.
        var input = (ComputeMethodInput)Input;
        var function = (ConsolidatingComputeMethodFunction)input.Function;
        var source = function.GetExistingSource(input.Invocation);
        source?.Invalidate(immediately: false, new InvalidationSource(this));
    }

    // Private methods

    private void OnSourceInvalidated(Computed invalidated)
    {
        // Double-check locking
        if (Volatile.Read(ref _whenConsolidated) is not null)
            return;

        lock (Lock) {
            // No need to consolidate: we're to be replaced anyway
            if (Volatile.Read(ref _whenConsolidated) is not null || !this.IsConsistent()) return;

            Task whenConsolidated;
            // The cascade bringing us here may run inside ProduceComputed, and Consolidate re-locks
            // the very input it produces - so its MarkLockedLocally marker must not flow into it.
            using (ExecutionContextExt.TrySuppressFlow())
                whenConsolidated = Task.Run(Consolidate, CancellationToken.None);
            // Release: the null check above and WhenConsolidated read this without the lock
            Volatile.Write(ref _whenConsolidated, whenConsolidated);
        }
        // _source.Invalidated is auto-removed on invalidation, so we don't need to bother about this
        return;

        async Task Consolidate() {
            Computed<T>? nextSource = null; // null means to Invalidate(), which is the default if this method fails
            try {
                try {
                    if (Options.ConsolidationDelay != TimeSpanExt.Infinite)
                        await Task.Delay(Options.ConsolidationDelay, CancellationToken.None).ConfigureAwait(false);

                    var updatedSource = (Computed<T>)await _source.UpdateUntyped(CancellationToken.None).ConfigureAwait(false);
                    nextSource = AreOutputsEqual(UntypedOutput, updatedSource.UntypedOutput)
                        ? updatedSource
                        : null; // Invalidate
                }
                finally {
                    if (nextSource is null) {
                        // No next source -> we're invalidating ourselves
                        // Invalidate(immediately: true) runs OnInvalidated synchronously, i.e. on this thread
                        InvalidatingConsolidationTarget = this;
                        try {
                            Invalidate(immediately: true, new InvalidationSource(_source));
                        }
                        finally {
                            InvalidatingConsolidationTarget = null;
                        }
                    }
                    else {
                        // 1. Re-enable the consolidation
                        lock (Lock) {
                            Volatile.Write(ref _source, nextSource);
                            // Stored last: the null re-enables consolidation, which reads _source
                            Volatile.Write(ref _whenConsolidated, null);
                        }
                        // 2. Subscribe to the next source's invalidation'
                        nextSource.Invalidated += OnSourceInvalidated;
                    }
                }
            }
            catch (Exception e) {
                // The finally above already applied the fallback, so we just observe & log the
                // fault here instead of leaving the _whenConsolidated task unobserved.
                var log = Input.Function.Services.LogFor(GetType());
                log.LogError(e, "Consolidation failed for {Category}", Input.Category);
            }
        }
    }

    private bool AreOutputsEqual(Result x, Result y)
    {
        // A custom comparer knows nothing about errors, so the default (error-aware) logic still applies to them
        if (x.Error is null && y.Error is null
            && ((ComputeMethodInput)Input).MethodDef.ConsolidationComparer is IEqualityComparer<T> comparer)
            return comparer.Equals((T)x.Value!, (T)y.Value!);

        return Input.Function.Hub.ComputedOutputEqualityComparer.Invoke(x, y);
    }
}
