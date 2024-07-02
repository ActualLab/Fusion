namespace ActualLab.Concurrency;

public class SequentialScheduler : TaskScheduler
{
    private readonly BlockingCollection<Task> _taskQueue = new();

    public Thread Thread { get; }
    public override int MaximumConcurrencyLevel => 1;

    public SequentialScheduler(bool useBackgroundThread = true, CancellationToken cancellationToken = default)
    {
        Thread = new Thread(() => Run(cancellationToken)) {
            IsBackground = useBackgroundThread,
        };
        Thread.Start();
    }

    public SequentialScheduler(Thread thread)
        => Thread = thread;

    public void Run(CancellationToken cancellationToken = default)
    {
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
        => _taskQueue.Add(task);

    protected override IEnumerable<Task> GetScheduledTasks()
        => _taskQueue;

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        => Thread.CurrentThread == Thread && TryExecuteTask(task);
}
