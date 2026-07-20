namespace ActualLab.Async;

/// <summary>
/// Coalesces concurrent runs of the same task factory: at most one run is in flight and at most
/// one more is queued behind it. Requests accepted while the queued run awaits its turn share
/// its task, so any burst of requests is served by at most two runs.
/// </summary>
public sealed class TaskCoalescer(Func<Task> taskFactory)
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private Task? _activeTask;
    private Task? _queuedTask;

    public ILogger? Log { get; init; }

    // The task covering every accepted request - e.g. to await on graceful shutdown
    public Task? LastTask {
        get {
            lock (_lock)
                return _queuedTask ?? _activeTask;
        }
    }

    public Task Invoke()
    {
        lock (_lock) {
            // The queued task must be checked first: the active one may be already completed
            // while the queued one hasn't promoted itself yet, and starting a new run
            // in this state would bypass the queued one
            if (_queuedTask is { } queuedTask)
                return queuedTask;
            if (_activeTask is { IsCompleted: false } activeTask)
                return _queuedTask = QueuedRun(activeTask);

            return _activeTask = taskFactory.Invoke();
        }
    }

    // Private methods

    private async Task QueuedRun(Task activeTask)
    {
        await activeTask.SilentAwait(false);
        // The yield forces an asynchronous continuation, which can't enter the promotion block
        // below before Invoke (still holding _lock) assigns _queuedTask = this task
        await Task.Yield();
        lock (_lock) {
            if (!ReferenceEquals(_activeTask, activeTask)) {
                // Must be unreachable; skipping the run is the recovery here
                Log?.LogCritical(
                    $"{nameof(TaskCoalescer)}.{nameof(QueuedRun)}: the task it chained itself behind isn't the active one");
                _queuedTask = null;
                return;
            }

            _activeTask = _queuedTask;
            _queuedTask = null;
        }
        await taskFactory.Invoke().ConfigureAwait(false);
    }
}
