namespace ActualLab.Async;

/// <summary>
/// A safer version of
/// https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync
/// that ensures <see cref="DisposeAsync(bool)"/> is called just once.
/// </summary>
public abstract class SafeAsyncDisposableBase : IAsyncDisposable, IDisposable, IHasWhenDisposed
{
    private volatile int _isDisposing;
    private volatile Task? _disposeTask;

    public bool IsDisposed => _disposeTask is not null;
    public Task? WhenDisposed => _disposeTask;

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _isDisposing, 1, 0) != 0)
            return;

        _ = StartDispose();
    }

    public ValueTask DisposeAsync()
    {
        Task? disposeTask;
        if (Interlocked.CompareExchange(ref _isDisposing, 1, 0) != 0) {
            var spinWait = new SpinWait();
            while (true) {
                disposeTask = _disposeTask;
                if (disposeTask is not null)
                    return disposeTask.ToValueTask();
                spinWait.SpinOnce(); // Safe for WASM
            }
        }

        disposeTask = StartDispose();
        return disposeTask.ToValueTask();
    }

    protected abstract Task DisposeAsync(bool disposing);

    private Task StartDispose()
    {
        Task disposeTask;
        try {
            disposeTask = DisposeAsync(true);
        }
        catch (Exception e) {
            disposeTask = Task.FromException(e);
        }
        _disposeTask = disposeTask;
        GC.SuppressFinalize(this);
        return disposeTask;
    }

    protected bool MarkDisposed()
    {
        if (Interlocked.CompareExchange(ref _isDisposing, 1, 0) != 0)
            return false;

        _disposeTask = Task.CompletedTask;
        GC.SuppressFinalize(this);
        return true;
    }
}
