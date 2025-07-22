namespace ActualLab.Async;

public static class WorkerExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TWorker Start<TWorker>(this TWorker worker)
        where TWorker : IWorker
    {
        _ = worker.Run();
        return worker;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TWorker Start<TWorker>(this TWorker worker, bool isolate)
        where TWorker : IWorker
    {
        _ = isolate
            ? ExecutionContextExt.Start(ExecutionContextExt.Default, worker.Run)
            : worker.Run();
        return worker;
    }
}
