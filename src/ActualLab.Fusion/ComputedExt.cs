using ActualLab.Caching;
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
        => (Task<T>)computed.UseUntyped(cancellationToken);

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

    // Perf: a copy of above method requiring no cast to interface
    public static void Invalidate<T>(this Computed<T> computed, TimeSpan delay, bool? usePreciseTimer = null)
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
            computed.Invalidated += static c => Timeouts.Generic.Remove((IGenericTimeoutHandler)c);
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

    // Perf: a copy of above method requiring no cast to interface
    public static Task WhenInvalidated<T>(this Computed<T> computed, CancellationToken cancellationToken = default)
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
                computed = await computed.Update<T>(cancellationToken).ConfigureAwait(false);
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

    // When w/ computedTask

    public static Task<Computed<T>> When<T>(
        this ValueTask<Computed<T>> computedTask,
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
        => computedTask.When(predicate, FixedDelayer.NextTick, cancellationToken);

    public static async Task<Computed<T>> When<T>(
        this ValueTask<Computed<T>> computedTask,
        Func<T, bool> predicate,
        IUpdateDelayer updateDelayer,
        CancellationToken cancellationToken = default)
    {
        var computed = await computedTask.ConfigureAwait(false);
        return await computed.When(predicate, updateDelayer, cancellationToken).ConfigureAwait(false);
    }

    public static Task<Computed<T>> When<T>(
        this ValueTask<Computed<T>> computedTask,
        Func<T, Exception?, bool> predicate,
        CancellationToken cancellationToken = default)
        => computedTask.When(predicate, FixedDelayer.NextTick, cancellationToken);

    public static async Task<Computed<T>> When<T>(
        this ValueTask<Computed<T>> computedTask,
        Func<T, Exception?, bool> predicate,
        IUpdateDelayer updateDelayer,
        CancellationToken cancellationToken = default)
    {
        var computed = await computedTask.ConfigureAwait(false);
        return await computed.When(predicate, updateDelayer, cancellationToken).ConfigureAwait(false);
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

    // WhenSynchronized & Synchronize

    public static Task WhenSynchronized(
        this Computed computed,
        CancellationToken cancellationToken = default)
    {
        if (computed is IMaybeCachedValue mcv)
            return mcv.WhenSynchronized.WaitAsync(cancellationToken);

        if (computed is IStateBoundComputed stateBoundComputed) {
            var state = stateBoundComputed.State;
            if (state is IMutableState)
                return Task.CompletedTask;

            var snapshot = state.Snapshot;
            if (snapshot.IsInitial)
                return WhenUpdatedAndSynchronized(snapshot, cancellationToken);

            static async Task WhenUpdatedAndSynchronized(StateSnapshot snapshot, CancellationToken cancellationToken1) {
                await snapshot.WhenUpdated().ConfigureAwait(false);
                await snapshot.State.Computed.WhenSynchronized(cancellationToken1).ConfigureAwait(false);
            }
        }

        // Computed is a regular computed instance
        var usedBuffer = ArrayBuffer<Computed>.Lease(false);
        var taskBuffer = ArrayBuffer<Task>.Lease(false);
        try {
            computed.CopyDependenciesTo(ref usedBuffer);
            var usedArray = usedBuffer.Buffer;
            for (var i = 0; i < usedBuffer.Count; i++) {
                var used = usedArray[i];
                var whenSynchronized = used.WhenSynchronized(cancellationToken);
                if (!whenSynchronized.IsCompleted)
                    taskBuffer.Add(whenSynchronized);
            }
            return taskBuffer.Count switch {
                0 => Task.CompletedTask,
                1 => taskBuffer[0],
                _ => Task.WhenAll(taskBuffer.ToArray()),
            };
        }
        finally {
            taskBuffer.Release();
            usedBuffer.Release();
        }
    }

    public static async ValueTask<Computed<T>> Synchronize<T>(
        this Computed<T> computed,
        CancellationToken cancellationToken = default)
    {
        while (true) {
            var whenSynchronized = computed.WhenSynchronized(cancellationToken);
            if (!whenSynchronized.IsCompleted)
                await whenSynchronized.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (computed.IsConsistent())
                return computed;

            computed = (Computed<T>)await computed.UpdateUntyped(cancellationToken).ConfigureAwait(false);
        }
    }
}
