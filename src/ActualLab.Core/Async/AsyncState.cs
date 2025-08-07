using ActualLab.Internal;

namespace ActualLab.Async;

public interface IAsyncState
{
    public bool IsFinal { get; }
    public bool HasNext { get; }
    public IAsyncState? Next { get; }
    public IAsyncState Last { get; }
    public IAsyncState LastNonFinal { get; }
    public Task WhenNext();
    public Task WhenNext(CancellationToken cancellationToken);
}

public interface IAsyncState<out T> : IAsyncState, IAsyncEnumerable<IAsyncState<T>>
{
    public T Value { get; }
    public new IAsyncState<T>? Next { get; }
    public new IAsyncState<T> Last { get; }
    public new IAsyncState<T> LastNonFinal { get; }

    public Task<IAsyncState> When(Func<T, bool> predicate, CancellationToken cancellationToken = default);
    public IAsyncEnumerable<T> Changes(CancellationToken cancellationToken = default);
}

public sealed class AsyncState<T>(T value)
    : IAsyncState<T>, IAsyncEnumerable<AsyncState<T>>
{
    private readonly AsyncTaskMethodBuilder<AsyncState<T>> _nextSource = AsyncTaskMethodBuilderExt.New<AsyncState<T>>();

    public T Value { get; } = value;
    public bool IsFinal => _nextSource.Task.IsFaultedOrCancelled();

    // Next
    IAsyncState? IAsyncState.Next => Next;
    IAsyncState<T>? IAsyncState<T>.Next => Next;
    public AsyncState<T>? Next => _nextSource.Task.IsCompleted ? _nextSource.Task.Result : null;
    public bool HasNext => _nextSource.Task.IsCompleted;

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

    // LastNonFinal
    IAsyncState IAsyncState.LastNonFinal => LastNonFinal;
    IAsyncState<T> IAsyncState<T>.LastNonFinal => LastNonFinal;
    public AsyncState<T> LastNonFinal {
        get {
            var current = this;
            while (current._nextSource.Task is { } nextTask && nextTask.IsCompletedSuccessfully())
                current = nextTask.Result;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    Task IAsyncState.WhenNext()
        => _nextSource.Task;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    Task IAsyncState.WhenNext(CancellationToken cancellationToken)
        => _nextSource.Task.WaitAsync(cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<AsyncState<T>> WhenNext()
        => _nextSource.Task;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<AsyncState<T>> WhenNext(CancellationToken cancellationToken)
        => _nextSource.Task.WaitAsync(cancellationToken);

    Task<IAsyncState> IAsyncState<T>.When(Func<T, bool> predicate, CancellationToken cancellationToken)
        => When(predicate, cancellationToken)
            .ContinueWith(
                static t => (IAsyncState)t.GetAwaiter().GetResult(),
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

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
        var next = new AsyncState<T>(value);
        // ReSharper disable once PossiblyImpureMethodCallOnReadonlyVariable
        _nextSource.SetResult(next);
        return next;
    }

    public AsyncState<T> TrySetNext(T value)
    {
        var next = new AsyncState<T>(value);
        return _nextSource.TrySetResult(next) ? next : this;
    }

    // SetFinal & TrySetFinal

    public void SetFinal(Exception error)
        // ReSharper disable once PossiblyImpureMethodCallOnReadonlyVariable
        => _nextSource.SetException(error);

    public void SetFinal(CancellationToken cancellationToken)
        => _nextSource.TrySetCanceled(cancellationToken);

    public bool TrySetFinal(Exception error)
        => _nextSource.TrySetException(error);

    public bool TrySetFinal(CancellationToken cancellationToken)
        => _nextSource.TrySetCanceled(cancellationToken);

    // RequireNonFinal

    public AsyncState<T> RequireNonFinal()
    {
        if (!IsFinal)
            return this;

        _ = _nextSource.Task.GetAwaiter().GetResult(); // Must throw in case there is an error
        throw Errors.AsyncStateIsFinal();
    }

    public AsyncState<T> RequireNonFinal(Func<Exception> errorFactory)
    {
        if (!IsFinal)
            return this;

        _ = _nextSource.Task.GetAwaiter().GetResult(); // Must throw in case there is an error
        throw errorFactory.Invoke();
    }
}
