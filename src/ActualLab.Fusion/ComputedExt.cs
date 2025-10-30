using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public static partial class ComputedExt
{
    // Update & Use

    public static async ValueTask<Computed<T>> Update<T>(
        this Computed<T> computed,
        CancellationToken cancellationToken = default)
    {
        var newComputed = await computed.UpdateUntyped(cancellationToken).ConfigureAwait(false);
        return (Computed<T>)newComputed;
    }

    public static Task<T> Use<T>(this Computed<T> computed, CancellationToken cancellationToken = default)
        => (Task<T>)computed.UseUntyped(allowInconsistent: false, cancellationToken);

    public static Task<T> Use<T>(this Computed<T> computed, bool allowInconsistent, CancellationToken cancellationToken = default)
        => (Task<T>)computed.UseUntyped(allowInconsistent, cancellationToken);

    // GetInvalidationTarget

    public static Computed? GetInvalidationTarget(this Computed computed)
        => computed is IInvalidationProxyComputed proxyComputed
            ? proxyComputed.InvalidationTarget
            : computed;

    // Invalidate

    public static void Invalidate(this Computed computed, TimeSpan delay, bool? usePreciseTimer = null)
    {
        if (delay == TimeSpan.MaxValue) // No invalidation
            return;

        if (delay <= TimeSpan.Zero) { // Instant invalidation
            computed.Invalidate();
            return;
        }

        var bPrecise = usePreciseTimer ?? delay <= Computed.PreciseInvalidationDelayThreshold;
        if (!bPrecise) {
            Timeouts.Generic.AddOrUpdateToEarlier(computed, Timeouts.Clock.Now + delay);
            computed.Invalidated += static c => Timeouts.Generic.Remove(c);
            return;
        }

        // CancellationTokenSource's continuations don't need the ExecutionContext,
        // plus it's a good thing to ditch it here to avoid possible memory leaks.
        using var _ = ExecutionContextExt.TrySuppressFlow();
        var cts = new CancellationTokenSource(delay);
        var registration = cts.Token.Register(() => {
            // No need to schedule this via Task.Run, since this code is
            // either invoked from Invalidate method (via Invalidated handler),
            // so Invalidate() call will do nothing & return immediately,
            // or it's invoked via one of timer threads, i.e. where it's
            // totally fine to invoke Invalidate directly as well.
            computed.Invalidate(true);
            cts.Dispose();
        });
        computed.Invalidated += _ => {
            try {
                if (!cts.IsCancellationRequested)
                    cts.Cancel(true);
            }
            catch {
                // Intended: this method should never throw any exceptions
            }
            finally {
                registration.Dispose();
                cts.Dispose();
            }
        };
    }

    // WhenInvalidated

    public static Task WhenInvalidated(this Computed computed, CancellationToken cancellationToken = default)
    {
        if (computed.ConsistencyState == ConsistencyState.Invalidated)
            return Task.CompletedTask;

        var tcs = AsyncTaskMethodBuilderExt.New();
        if (cancellationToken.CanBeCanceled) {
            cancellationToken.ThrowIfCancellationRequested();
            return new WhenInvalidatedClosure(tcs, computed, cancellationToken).Task;
        }

        // No way to cancel / unregister the handler here
        computed.Invalidated += _ => tcs.TrySetResult();
        return tcs.Task;
    }

    // When

    public static Task<Computed<T>> When<T>(this Computed<T> computed,
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
        => computed.When(predicate, FixedDelayer.NextTick, cancellationToken);

    public static async Task<Computed<T>> When<T>(this Computed<T> computed,
        Func<T, bool> predicate,
        IUpdateDelayer updateDelayer,
        CancellationToken cancellationToken = default)
    {
        while (true) {
            if (!computed.IsConsistent())
                computed = await computed.Update(cancellationToken).ConfigureAwait(false);
            if (predicate.Invoke(computed.Value))
                return computed;

            await computed.WhenInvalidated(cancellationToken).ConfigureAwait(false);
            await updateDelayer.Delay(0, cancellationToken).ConfigureAwait(false);
        }
    }

    public static Task<Computed<T>> When<T>(this Computed<T> computed,
        Func<T, Exception?, bool> predicate,
        CancellationToken cancellationToken = default)
        => computed.When(predicate, FixedDelayer.NextTick, cancellationToken);

    public static async Task<Computed<T>> When<T>(this Computed<T> computed,
        Func<T, Exception?, bool> predicate,
        IUpdateDelayer updateDelayer,
        CancellationToken cancellationToken = default)
    {
        while (true) {
            if (!computed.IsConsistent())
                computed = (Computed<T>)await computed.UpdateUntyped(cancellationToken).ConfigureAwait(false);
            var (value, error) = computed;
            if (predicate.Invoke(value, error))
                return computed;

            await computed.WhenInvalidated(cancellationToken).ConfigureAwait(false);
            await updateDelayer.Delay(0, cancellationToken).ConfigureAwait(false);
        }
    }

    // WhenUntyped

    public static Task<Computed> WhenUntyped(this Computed computed,
        Func<Computed, bool> predicate,
        CancellationToken cancellationToken = default)
        => computed.WhenUntyped(predicate, FixedDelayer.NextTick, cancellationToken);

    public static async Task<Computed> WhenUntyped(this Computed computed,
        Func<Computed, bool> predicate,
        IUpdateDelayer updateDelayer,
        CancellationToken cancellationToken = default)
    {
        while (true) {
            if (!computed.IsConsistent())
                computed = await computed.UpdateUntyped(cancellationToken).ConfigureAwait(false);
            if (predicate.Invoke(computed))
                return computed;

            await computed.WhenInvalidated(cancellationToken).ConfigureAwait(false);
            await updateDelayer.Delay(0, cancellationToken).ConfigureAwait(false);
        }
    }

    // Changes

    public static IAsyncEnumerable<Computed<T>> Changes<T>(
        this Computed<T> computed,
        CancellationToken cancellationToken = default)
        => computed.Changes(FixedDelayer.NextTick, cancellationToken);

    public static async IAsyncEnumerable<Computed<T>> Changes<T>(
        this Computed<T> computed,
        IUpdateDelayer updateDelayer,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var retryCount = 0;
        while (true) {
            computed = (Computed<T>)await computed.UpdateUntyped(cancellationToken).ConfigureAwait(false);
            yield return computed;

            await computed.WhenInvalidated(cancellationToken).ConfigureAwait(false);

            var hasTransientError = computed.Error is { } error && computed.IsTransientError(error);
            retryCount = hasTransientError ? retryCount + 1 : 0;

            await updateDelayer.Delay(retryCount, cancellationToken).ConfigureAwait(false);
        }
        // ReSharper disable once IteratorNeverReturns
    }

    // ChangesUntyped

    public static IAsyncEnumerable<Computed> ChangesUntyped(
        this Computed computed,
        CancellationToken cancellationToken = default)
        => computed.ChangesUntyped(FixedDelayer.NextTick, cancellationToken);

    public static async IAsyncEnumerable<Computed> ChangesUntyped(
        this Computed computed,
        IUpdateDelayer updateDelayer,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var retryCount = 0;
        while (true) {
            computed = await computed.UpdateUntyped(cancellationToken).ConfigureAwait(false);
            yield return computed;

            await computed.WhenInvalidated(cancellationToken).ConfigureAwait(false);

            var hasTransientError = computed.Error is { } error && computed.IsTransientError(error);
            retryCount = hasTransientError ? retryCount + 1 : 0;

            await updateDelayer.Delay(retryCount, cancellationToken).ConfigureAwait(false);
        }
        // ReSharper disable once IteratorNeverReturns
    }

    // IsSynchronized, WhenSynchronized, Synchronize

    public static bool IsSynchronized(this Computed computed)
        => ComputedSynchronizer.Current.IsSynchronized(computed);

    public static bool IsSynchronized(this Computed computed, ComputedSynchronizer computedSynchronizer)
        => computedSynchronizer.IsSynchronized(computed);

    public static Task WhenSynchronized(
        this Computed computed,
        CancellationToken cancellationToken = default)
        => ComputedSynchronizer.Current.WhenSynchronized(computed, cancellationToken);

    public static Task WhenSynchronized(
        this Computed computed,
        ComputedSynchronizer computedSynchronizer,
        CancellationToken cancellationToken = default)
        => computedSynchronizer.WhenSynchronized(computed, cancellationToken);

    public static ValueTask<Computed> Synchronize(
        this Computed computed,
        CancellationToken cancellationToken = default)
        => ComputedSynchronizer.Current.Synchronize(computed, cancellationToken);

    public static ValueTask<Computed> Synchronize(
        this Computed computed,
        ComputedSynchronizer computedSynchronizer,
        CancellationToken cancellationToken = default)
        => computedSynchronizer.Synchronize(computed, cancellationToken);

    public static ValueTask<Computed<T>> Synchronize<T>(
        this Computed<T> computed,
        CancellationToken cancellationToken = default)
        => computed.Synchronize(ComputedSynchronizer.Current, cancellationToken);

    public static async ValueTask<Computed<T>> Synchronize<T>(
        this Computed<T> computed,
        ComputedSynchronizer computedSynchronizer,
        CancellationToken cancellationToken = default)
        => (Computed<T>)await computedSynchronizer.Synchronize(computed, cancellationToken).ConfigureAwait(false);
}
