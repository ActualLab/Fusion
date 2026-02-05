using Microsoft.Extensions.Hosting;

namespace ActualLab.Async;

/// <summary>
/// Base class for background workers with start/stop lifecycle management.
/// </summary>
public abstract class WorkerBase(CancellationTokenSource? stopTokenSource = null)
    : ProcessorBase(stopTokenSource), IWorker
{
    private volatile Task? _whenRunning;

    protected bool FlowExecutionContext { get; init; } = false;

    // WhenRunning should always return a task that never fails or gets cancelled
    public Task? WhenRunning => _whenRunning;

    protected override Task DisposeAsyncCore()
        => WhenRunning ?? Task.CompletedTask;

    // Returns a task that always succeeds
    public Task Run()
    {
        if (_whenRunning is not null)
            return _whenRunning;
        lock (Lock) {
#pragma warning disable MA0100
            if (_whenRunning is not null)
                return _whenRunning;

            if (StopToken.IsCancellationRequested || WhenDisposed is not null) {
                // We behave here like if OnStart() was cancelled right in the very beginning.
                // In this case _whenRunning would store a task that successfully completed.
                return _whenRunning = Task.CompletedTask;
            }
#pragma warning restore MA0100

            using var _ = FlowExecutionContext ? default : ExecutionContextExt.TrySuppressFlow();
            Task onStartTask;
            try {
                onStartTask = OnStart(StopToken);
            }
            catch (OperationCanceledException oce) {
                onStartTask = Task.FromCanceled(oce.CancellationToken);
            }
            catch (Exception e) {
                onStartTask = Task.FromException(e);
            }
            // ReSharper disable once PossibleMultipleWriteAccessInDoubleCheckLocking
            _whenRunning = Task.Run(async () => {
                try {
                    try {
                        await onStartTask.ConfigureAwait(false);
                        await OnRun(StopToken).ConfigureAwait(false);
                    }
                    finally {
                        StopTokenSource.CancelAndDisposeSilently();
                        await OnStop().ConfigureAwait(false);
                    }
                }
                catch {
                    // Intended: WhenRunning is returned by DisposeAsyncCore, so it should never throw
                }
            }, default);
        }
        return _whenRunning;
    }

    protected abstract Task OnRun(CancellationToken cancellationToken);
    protected virtual Task OnStart(CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task OnStop() => Task.CompletedTask;

    public Task Stop()
    {
        StopTokenSource.CancelAndDisposeSilently();
        return WhenRunning ?? Task.CompletedTask;
    }

    // IHostedService implementation

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
#pragma warning disable MA0040
        _ = Run();
#pragma warning restore MA0040
        return Task.CompletedTask;
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
        => Stop();
}
