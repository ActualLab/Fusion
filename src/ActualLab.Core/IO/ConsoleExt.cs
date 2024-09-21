using ActualLab.Concurrency;

namespace ActualLab.IO;

public static class ConsoleExt
{
    private static readonly Lock Lock = new();
    private static TaskScheduler? _scheduler;

    public static TaskScheduler Scheduler {
        get {
            if (_scheduler == null) {
                lock (Lock)
                    _scheduler ??= new SequentialScheduler();
            }
            return _scheduler;
        }
    }

    public static Task<string?> ReadLineAsync()
    {
        var taskFactory = new TaskFactory();
        return taskFactory.StartNew(ReadLine, CancellationToken.None, TaskCreationOptions.None, Scheduler);
    }
}
