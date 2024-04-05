namespace ActualLab.Fusion.EntityFramework.Operations;

public abstract class DbShardWatcher : ProcessorBase
{
    private TaskCompletionSource<Unit> _nextEventSource = null!;
    protected DbShard Shard { get; }

    protected DbShardWatcher(DbShard shard)
    {
        Shard = shard;
        // ReSharper disable once VirtualMemberCallInConstructor
        ReplaceNextEventTask();
    }

    public Task WaitForChanges(CancellationToken cancellationToken)
    {
        lock (Lock) {
            var task = _nextEventSource;
            if (_nextEventSource.Task.IsCompleted)
                ReplaceNextEventTask();
            return task.Task.WaitAsync(cancellationToken);
        }
    }

    protected void CompleteWaitForChanges()
    {
        lock (Lock)
            _nextEventSource.TrySetResult(default);
    }

    private void ReplaceNextEventTask()
        => _nextEventSource = TaskCompletionSourceExt.NewSynchronous<Unit>();
}
