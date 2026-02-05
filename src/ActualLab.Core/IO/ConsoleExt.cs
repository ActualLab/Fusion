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
    private static volatile TaskScheduler? _scheduler;

    public static TaskScheduler Scheduler {
        get {
            if (_scheduler is { } scheduler)
                return scheduler;
            lock (StaticLock)
                // ReSharper disable once NonAtomicCompoundOperator
                return _scheduler ??= new DedicatedThreadScheduler();
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
