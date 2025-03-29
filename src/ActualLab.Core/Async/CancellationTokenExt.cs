namespace ActualLab.Async;

#pragma warning disable CA1068

public static class CancellationTokenExt
{
    public static readonly CancellationToken Canceled;

    static CancellationTokenExt()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        Canceled = cts.Token;
    }

    public static string Format(this CancellationToken cancellationToken)
        => cancellationToken.CanBeCanceled
            ? $"ct-{(uint)cancellationToken.GetHashCode():x}"
            : "ct-none";

    // LinkWith

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CancellationTokenSource LinkWith(this CancellationToken token1, CancellationToken token2)
        => CancellationTokenSource.CreateLinkedTokenSource(token1, token2);

    public static CancellationTokenSource LinkWith(this CancellationToken token1, CancellationToken token2, TimeSpan cancelAfter)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(token1, token2);
        cts.CancelAfter(cancelAfter);
        return cts;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CancellationTokenSource LinkWith(this CancellationToken token1, CancellationToken token2, TimeSpan? cancelAfter)
        => cancelAfter is { } vCancelAfter
            ? token1.LinkWith(token2, vCancelAfter)
            : token1.LinkWith(token2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CancellationTokenSource CreateLinkedTokenSource(this CancellationToken cancellationToken)
        => CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

    public static CancellationTokenSource CreateLinkedTokenSource(this CancellationToken cancellationToken, TimeSpan cancelAfter)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(cancelAfter);
        return cts;
    }

    public static CancellationTokenSource CreateLinkedTokenSource(this CancellationToken cancellationToken, TimeSpan? cancelAfter)
        => cancelAfter is { } vCancelAfter
            ? cancellationToken.CreateLinkedTokenSource(vCancelAfter)
            : cancellationToken.CreateLinkedTokenSource();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CancellationTokenSource CreateDelayedTokenSource(
        this CancellationToken cancellationToken,
        TimeSpan cancellationDelay)
        => cancellationDelay > TimeSpan.Zero
            ? new DelayedCancellationTokenSource(cancellationToken, cancellationDelay)
            : cancellationToken.CreateLinkedTokenSource();

    // FromTask

    public static CancellationToken FromTask(Task task, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.CanBeCanceled) {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var result = cts.Token;
            result.Register(static state => (state as CancellationTokenSource).CancelAndDisposeSilently(), cts);
            _ = task.ContinueWith(_ => cts.Cancel(), TaskScheduler.Default);
            return result;
        }
        else {
            var cts = new CancellationTokenSource();
            var result = cts.Token;
            _ = task.ContinueWith(_ => cts.CancelAndDisposeSilently(), TaskScheduler.Default);
            return result;
        }
    }

    // ToTask

    public static Disposable<Task, (AsyncTaskMethodBuilder, CancellationToken, CancellationTokenRegistration)> ToTask(
        this CancellationToken token,
        bool runContinuationsAsynchronously = true)
    {
        var tcs = AsyncTaskMethodBuilderExt.New(runContinuationsAsynchronously);
        var r = token.Register(() => tcs.TrySetCanceled(token));
        return Disposable.New(tcs.Task, (tcs, token, r), (_, state) => {
#if NETSTANDARD
            state.r.Dispose();
#else
            state.r.Unregister();
#endif
            state.tcs.TrySetCanceled(state.token);
        });
    }

    public static Disposable<Task<T>, (AsyncTaskMethodBuilder<T>, CancellationToken, CancellationTokenRegistration)> ToTask<T>(
        this CancellationToken token,
        bool runContinuationsAsynchronously = true)
    {
        var tcs = AsyncTaskMethodBuilderExt.New<T>(runContinuationsAsynchronously);
        var r = token.Register(() => tcs.TrySetCanceled(token));
        return Disposable.New(tcs.Task, (tcs, token, r), (_, state) => {
#if NETSTANDARD
            state.r.Dispose();
#else
            state.r.Unregister();
#endif
            state.tcs.TrySetCanceled(state.token);
        });
    }

    // ToTaskUnsafe

    internal static Task ToTaskUnsafe(
        this CancellationToken token,
        bool runContinuationsAsynchronously = true)
    {
        var tcs = AsyncTaskMethodBuilderExt.New(runContinuationsAsynchronously);
        token.Register(() => tcs.TrySetCanceled(token));
        return tcs.Task;
    }

    internal static Task<T> ToTaskUnsafe<T>(
        this CancellationToken token,
        bool runContinuationsAsynchronously = true)
    {
        var tcs = AsyncTaskMethodBuilderExt.New<T>(runContinuationsAsynchronously);
        token.Register(() => tcs.TrySetCanceled(token));
        return tcs.Task;
    }

    // Nested types

    private sealed class DelayedCancellationTokenSource : CancellationTokenSource
    {
        private readonly TimeSpan _cancellationDelay;
        private readonly CancellationTokenRegistration _registration;

#pragma warning disable CA1068
        public DelayedCancellationTokenSource(CancellationToken cancellationToken, TimeSpan cancellationDelay)
#pragma warning restore CA1068
        {
            _cancellationDelay = cancellationDelay;
            _registration = cancellationToken.Register(static state => {
                var self = (DelayedCancellationTokenSource)state!;
                self.CancelAfter(self._cancellationDelay);
            }, this);
        }

        protected override void Dispose(bool disposing)
        {
            _registration.Dispose();
            base.Dispose(disposing);
        }
    }
}
