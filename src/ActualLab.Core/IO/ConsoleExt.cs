using ActualLab.Concurrency;

namespace ActualLab.IO;

/// <summary>
/// Extension methods for <see cref="Console"/> providing asynchronous console I/O.
/// </summary>
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
            lock (StaticLock) {
                if (_scheduler is { } newScheduler)
                    return newScheduler;

                newScheduler = new DedicatedThreadScheduler();
                Volatile.Write(ref _scheduler, newScheduler);
                return newScheduler;
            }
        }
    }

    public static Task<string?> ReadLineAsync()
    {
        var taskFactory = new TaskFactory(Scheduler);
#pragma warning disable CA2008
        return taskFactory.StartNew(ReadLine);
#pragma warning restore CA2008
    }
}
