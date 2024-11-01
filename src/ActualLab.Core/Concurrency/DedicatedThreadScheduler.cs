namespace ActualLab.Concurrency;

public class DedicatedThreadScheduler : TaskScheduler
{
    private readonly BlockingCollection<Task> _taskQueue = new();

    public Thread Thread { get; }
    public CancellationToken StopToken { get; }
    public override int MaximumConcurrencyLevel => 1;

    public DedicatedThreadScheduler(bool useBackgroundThread = true, CancellationToken stopToken = default)
    {
        StopToken = stopToken;
        Thread = new Thread(Run) { IsBackground = useBackgroundThread };
        Thread.Start();
    }

    public DedicatedThreadScheduler(Thread thread, CancellationToken stopToken = default)
    {
        StopToken = stopToken;
        Thread = thread;
    }

    public void Run()
    {
        var cancellationToken = StopToken;
        while (!cancellationToken.IsCancellationRequested) {
            try {
                var task = _taskQueue.Take(cancellationToken);
                TryExecuteTask(task);
            }
            catch {
                // ignored
            }
        }
    }

    // Protected & private methods

    protected override void QueueTask(Task task)
        => _taskQueue.Add(task, StopToken);

    protected override IEnumerable<Task> GetScheduledTasks()
        => _taskQueue;

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        => Thread.CurrentThread == Thread && TryExecuteTask(task);
}
