using System.Diagnostics.CodeAnalysis;
using ActualLab.Concurrency;

namespace ActualLab.IO;

public static class ConsoleExt
{
    private static readonly Lock StaticLock = LockFactory.Create();

    [field: AllowNull, MaybeNull]
    public static TaskScheduler Scheduler {
        get {
            if (field is { } scheduler)
                return scheduler;
            lock (StaticLock)
                return field ??= new DedicatedThreadScheduler();
        }
    }

    public static Task<string?> ReadLineAsync()
    {
        var taskFactory = new TaskFactory(Scheduler);
        return taskFactory.StartNew(ReadLine);
    }
}
