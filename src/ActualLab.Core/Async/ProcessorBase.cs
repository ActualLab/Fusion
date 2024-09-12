namespace ActualLab.Async;

public abstract class ProcessorBase : IAsyncDisposable, IDisposable, IHasWhenDisposed
{
    private volatile Task? _disposeTask;

#if NET9_0_OR_GREATER
    protected Lock Lock { get; } = new();
#else
    protected object Lock { get; } = new();
#endif
    protected CancellationTokenSource StopTokenSource { get; }

    public CancellationToken StopToken { get; }
    public bool IsDisposed => _disposeTask != null;
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
        Task disposeTask;
        lock (Lock) {
            if (_disposeTask == null) {
                StopTokenSource.CancelAndDisposeSilently();
                _disposeTask = DisposeAsyncCore();
            }
            disposeTask = _disposeTask;
        }
        await disposeTask.ConfigureAwait(false);
    }

    protected virtual Task DisposeAsyncCore()
        => Task.CompletedTask;
}
