namespace ActualLab.Async;

/// <summary>
/// Base class for async processors with built-in disposal and stop token support.
/// </summary>
public abstract class ProcessorBase : IAsyncDisposable, IDisposable, IHasWhenDisposed
{
    // Written under Lock, but IsDisposed / WhenDisposed read it lock-free
    private Task? _disposeTask;

#if NET9_0_OR_GREATER
    protected Lock Lock { get; } = new();
#else
    protected object Lock { get; } = new();
#endif
    protected CancellationTokenSource StopTokenSource { get; }

    public CancellationToken StopToken { get; }
    public bool IsDisposed => _disposeTask is not null;
    public Task? WhenDisposed => _disposeTask;

    protected ProcessorBase(CancellationTokenSource? stopTokenSource = null)
    {
        StopTokenSource = stopTokenSource ?? new CancellationTokenSource();
        StopToken = StopTokenSource.Token;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
        => _ = DisposeAsync();

    public async ValueTask DisposeAsync()
    {
        Task? disposeTask;
        lock (Lock) {
            disposeTask = _disposeTask;
            if (disposeTask is null) {
                StopTokenSource.CancelAndDisposeSilently();
                disposeTask = DisposeAsyncCore();
                Volatile.Write(ref _disposeTask, disposeTask);
            }
        }
        await disposeTask.ConfigureAwait(false);
    }

    protected virtual Task DisposeAsyncCore()
        => Task.CompletedTask;
}
