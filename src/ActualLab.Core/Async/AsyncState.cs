namespace ActualLab.Async;

public interface IAsyncState
{
    bool IsFinal { get; }
    IAsyncState? Next { get; }
    IAsyncState Last { get; }
    Task<IAsyncState> WhenNext(CancellationToken cancellationToken = default);
}

public interface IAsyncState<out T> : IAsyncState, IAsyncEnumerable<IAsyncState<T>>
{
    T Value { get; }
    new IAsyncState<T>? Next { get; }
    new IAsyncState<T> Last { get; }

    Task<IAsyncState> When(Func<T, bool> predicate, CancellationToken cancellationToken = default);
    IAsyncEnumerable<T> Changes(CancellationToken cancellationToken = default);
}

public sealed class AsyncState<T>(T value, bool runContinuationsAsynchronously)
    : IAsyncState<T>, IAsyncEnumerable<AsyncState<T>>
{
    private readonly TaskCompletionSource<AsyncState<T>> _next
        = TaskCompletionSourceExt.New<AsyncState<T>>(runContinuationsAsynchronously);

    public T Value { get; } = value;
    public bool IsFinal => _next.Task.IsFaultedOrCancelled();

    // Next
    IAsyncState? IAsyncState.Next => Next;
    IAsyncState<T>? IAsyncState<T>.Next => Next;
    public AsyncState<T>? Next => _next.Task.IsCompleted ? _next.Task.Result : null;

    // Last
    IAsyncState IAsyncState.Last => Last;
    IAsyncState<T> IAsyncState<T>.Last => Last;
    public AsyncState<T> Last {
        get {
            var current = this;
            while (current.Next is { } next)
                current = next;
            return current;
        }
    }

    // GetAsyncEnumerator

    async IAsyncEnumerator<IAsyncState<T>> IAsyncEnumerable<IAsyncState<T>>.GetAsyncEnumerator(
        CancellationToken cancellationToken)
    {
        var current = this;
        while (true) {
            yield return current;
            current = await current.WhenNext(cancellationToken).ConfigureAwait(false);
        }
        // ReSharper disable once IteratorNeverReturns
    }

    async IAsyncEnumerator<AsyncState<T>> IAsyncEnumerable<AsyncState<T>>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        var current = this;
        while (true) {
            yield return current;
            current = await current.WhenNext(cancellationToken).ConfigureAwait(false);
        }
        // ReSharper disable once IteratorNeverReturns
    }

    // ToString

    public override string ToString()
        => $"{GetType().GetName()}({Value})";

    // WhenNext

    async Task<IAsyncState> IAsyncState.WhenNext(CancellationToken cancellationToken)
        => await WhenNext(cancellationToken).ConfigureAwait(false);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<AsyncState<T>> WhenNext(CancellationToken cancellationToken = default)
        => _next.Task.WaitAsync(cancellationToken);

    async Task<IAsyncState> IAsyncState<T>.When(Func<T, bool> predicate, CancellationToken cancellationToken)
        => await When(predicate, cancellationToken).ConfigureAwait(false);
    public async Task<AsyncState<T>> When(Func<T, bool> predicate, CancellationToken cancellationToken = default)
    {
        var current = this;
        while (!predicate.Invoke(current.Value))
            current = await current.WhenNext(cancellationToken).ConfigureAwait(false);
        return current;
    }

    // Changes

    public async IAsyncEnumerable<T> Changes(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var current = this;
        while (true) {
            yield return current.Value;
            current = await current.WhenNext(cancellationToken).ConfigureAwait(false);
        }
        // ReSharper disable once IteratorNeverReturns
    }

    // SetNext & TrySetNext

    public AsyncState<T> SetNext(T value)
    {
        var next = new AsyncState<T>(value, runContinuationsAsynchronously);
        _next.SetResult(next);
        return next;
    }

    public AsyncState<T> TrySetNext(T value)
    {
        var next = new AsyncState<T>(value, runContinuationsAsynchronously);
        return _next.TrySetResult(next) ? next : this;
    }

    // SetFinal & TrySetFinal

    public void SetFinal(Exception error)
        => _next.SetException(error);

    public void SetFinal(CancellationToken cancellationToken)
    {
#if NET5_0_OR_GREATER
        _next.SetCanceled(cancellationToken);
#else
        _next.SetCanceled();
#endif
    }

    public bool TrySetFinal(Exception error)
        => _next.TrySetException(error);

    public bool TrySetFinal(CancellationToken cancellationToken)
        => _next.TrySetCanceled(cancellationToken);
}
