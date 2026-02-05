namespace ActualLab.Async;

/// <summary>
/// Extension methods for <see cref="IWorker"/>.
/// </summary>
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

    public static Task Run(this IWorker worker, CancellationToken cancellationToken)
        => worker.Run(isolate: false, cancellationToken);

    public static Task Run(this IWorker worker, bool isolate, CancellationToken cancellationToken)
    {
        if (cancellationToken.CanBeCanceled)
            return RunAsync(worker, isolate, cancellationToken);

        worker.Start(isolate);
#pragma warning disable MA0040
        return worker.Run();
#pragma warning restore MA0040

        static async Task RunAsync(IWorker worker, bool isolate, CancellationToken cancellationToken) {
#pragma warning disable MA0134
            var registration = cancellationToken.Register(() => worker.Stop());
#pragma warning restore MA0134
            try {
                worker.Start(isolate);
#pragma warning disable MA0040
                await worker.Run().ConfigureAwait(isolate);
#pragma warning restore MA0040
            }
            finally {
                // ReSharper disable once MethodHasAsyncOverload
                registration.Dispose();
            }
        }
    }
}
