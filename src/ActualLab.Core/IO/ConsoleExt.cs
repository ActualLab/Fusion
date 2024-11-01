using ActualLab.Concurrency;

namespace ActualLab.IO;

public static class ConsoleExt
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static TaskScheduler? _scheduler;

    public static TaskScheduler Scheduler {
        get {
            if (_scheduler is { } scheduler)
                return scheduler;
            lock (StaticLock)
                return _scheduler ??= new DedicatedThreadScheduler();
        }
    }

    public static Task<string?> ReadLineAsync()
    {
        var taskFactory = new TaskFactory(Scheduler);
        return taskFactory.StartNew(ReadLine);
    }
}
