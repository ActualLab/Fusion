namespace ActualLab.Async;

#pragma warning disable MA0004

public static partial class TaskExt
{
    // Collect - a bit more user-friendly version of Task.WhenAll

    // Collect - Task version

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Collect(
        this IEnumerable<Task> tasks, CancellationToken cancellationToken = default)
        => tasks.Collect(0, true, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Collect(
        this IEnumerable<Task> tasks, int concurrency, CancellationToken cancellationToken = default)
        => tasks.Collect(concurrency, true, cancellationToken);

    public static Task Collect(
        this IEnumerable<Task> tasks, int concurrency, bool useCurrentScheduler, CancellationToken cancellationToken = default)
        => concurrency <= 0
            ? Task.WhenAll(tasks)
            : CollectConcurrently(tasks, concurrency, useCurrentScheduler, cancellationToken);

    // Collect - Task<T> version

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<T[]> Collect<T>(
        this IEnumerable<Task<T>> tasks, CancellationToken cancellationToken = default)
        => tasks.Collect(0, true, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<T[]> Collect<T>(
        this IEnumerable<Task<T>> tasks, int concurrency, CancellationToken cancellationToken = default)
        => tasks.Collect(concurrency, true, cancellationToken);

    public static Task<T[]> Collect<T>(
        this IEnumerable<Task<T>> tasks, int concurrency, bool useCurrentScheduler, CancellationToken cancellationToken = default)
        => concurrency <= 0
            ? Task.WhenAll(tasks)
            : CollectConcurrently(tasks, concurrency, useCurrentScheduler, cancellationToken);

    // CollectResults

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<Result<T>[]> CollectResults<T>(
        this IEnumerable<Task<T>> tasks, CancellationToken cancellationToken = default)
        => tasks.CollectResults(0, true, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<Result<T>[]> CollectResults<T>(
        this IEnumerable<Task<T>> tasks, int concurrency, CancellationToken cancellationToken = default)
        => tasks.CollectResults(concurrency, true, cancellationToken);

    public static Task<Result<T>[]> CollectResults<T>(
        this IEnumerable<Task<T>> tasks, int concurrency, bool useCurrentScheduler, CancellationToken cancellationToken = default)
        => concurrency <= 0
            ? ToResults(tasks.ToList(), cancellationToken)
            : CollectResultsConcurrently(tasks, concurrency, useCurrentScheduler, cancellationToken);

    // Private methods

    private static async Task<T[]> CollectConcurrently<T>(
        this IEnumerable<Task<T>> tasks, int concurrency, bool useCurrentScheduler, CancellationToken cancellationToken)
    {
        var semaphore = new SemaphoreSlim(concurrency);
        Action releaseSemaphore = () => semaphore.Release();
        var buffer = ArrayBuffer<Task<T>>.Lease(true, concurrency * 2);
        try {
            foreach (var task in tasks) {
                buffer.Add(task);
                if (!task.IsCompleted) {
                    var waitTask = semaphore.WaitAsync(cancellationToken); // Acquire 1
                    task.GetAwaiter().OnCompleted(releaseSemaphore); // Release 1 when task completes
                    await waitTask.ConfigureAwait(useCurrentScheduler); // Await for the 1 acquired
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return buffer.Count == 0
                ? []
                : await Task.WhenAll(buffer.ToArray()).ConfigureAwait(false);
        }
        finally {
            buffer.Release();
        }
    }

    private static async Task CollectConcurrently(
        this IEnumerable<Task> tasks, int concurrency, bool useCurrentScheduler, CancellationToken cancellationToken)
    {
        var semaphore = new SemaphoreSlim(concurrency);
        Action releaseSemaphore = () => semaphore.Release();
        var buffer = ArrayBuffer<Task>.Lease(true, concurrency * 2);
        try {
            foreach (var task in tasks) {
                buffer.Add(task);
                if (!task.IsCompleted) {
                    var waitTask = semaphore.WaitAsync(cancellationToken); // Acquire 1
                    task.GetAwaiter().OnCompleted(releaseSemaphore); // Release 1 when task completes
                    await waitTask.ConfigureAwait(useCurrentScheduler); // Await for the 1 acquired
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (buffer.Count != 0)
                await Task.WhenAll(buffer.ToArray()).ConfigureAwait(false);
        }
        finally {
            buffer.Release();
        }
    }

    private static async Task<Result<T>[]> CollectResultsConcurrently<T>(
        this IEnumerable<Task<T>> tasks, int concurrency, bool useCurrentScheduler, CancellationToken cancellationToken)
    {
        var semaphore = new SemaphoreSlim(concurrency);
        Action releaseSemaphore = () => semaphore.Release();
        var buffer = ArrayBuffer<Task<T>>.Lease(true, concurrency * 2);
        try {
            foreach (var task in tasks) {
                buffer.Add(task);
                if (!task.IsCompleted) {
                    var waitTask = semaphore.WaitAsync(cancellationToken); // Acquire 1
                    task.GetAwaiter().OnCompleted(releaseSemaphore); // Release 1 when task completes
                    await waitTask.ConfigureAwait(useCurrentScheduler); // Await for the 1 acquired
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return buffer.Count == 0
                ? []
                : await ToResults(buffer.ToArray(), cancellationToken).ConfigureAwait(false);
        }
        finally {
            buffer.Release();
        }
    }

    private static async Task<Result<T>[]> ToResults<T>(
        IReadOnlyList<Task<T>> tasks, CancellationToken cancellationToken)
    {
        await Task.WhenAll(tasks).SilentAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var result = new Result<T>[tasks.Count];
        for (var i = 0; i < result.Length; i++)
            result[i] = tasks[i].ToResultSynchronously();
        return result;
    }
}
