namespace ActualLab.Async;

#pragma warning disable MA0004

public static partial class TaskExt
{
    // Collect - a bit more user-friendly version of Task.WhenAll

    public static Task<T[]> Collect<T>(this IEnumerable<Task<T>> tasks, int concurrency = 0)
        => concurrency <= 0 ? Task.WhenAll(tasks) : CollectConcurrently(tasks, concurrency);

    public static Task Collect(this IEnumerable<Task> tasks, int concurrency = 0)
        => concurrency <= 0 ? Task.WhenAll(tasks) : CollectConcurrently(tasks, concurrency);

    public static Task<Result<T>[]> CollectResults<T>(this IEnumerable<Task<T>> tasks, int concurrency = 0)
        => concurrency <= 0 ? ToResults(tasks.ToArray()) : CollectResultsConcurrently(tasks, concurrency);

    // Private methods

    private static async Task<T[]> CollectConcurrently<T>(this IEnumerable<Task<T>> tasks, int concurrency)
    {
        var buffer = ArrayBuffer<Task<T>>.Lease(true, concurrency * 2);
        try {
            var runningCount = 0;
            var i = 0;
            foreach (var task in tasks) {
                buffer.Add(task);
                if (concurrency > runningCount)
                    runningCount++;
                else
                    await buffer[i++].SilentAwait(false);
            }
            return buffer.Count == 0
                ? Array.Empty<T>()
                : await Task.WhenAll(buffer.ToArray()).ConfigureAwait(false);
        }
        finally {
            buffer.Release();
        }
    }

    private static async Task CollectConcurrently(this IEnumerable<Task> tasks, int concurrency)
    {
        var buffer = ArrayBuffer<Task>.Lease(true, concurrency * 2);
        try {
            var runningCount = 0;
            var i = 0;
            foreach (var task in tasks) {
                buffer.Add(task);
                if (concurrency > runningCount)
                    runningCount++;
                else
                    await buffer[i++].SilentAwait(false);
            }
            if (buffer.Count != 0)
                await Task.WhenAll(buffer.ToArray()).ConfigureAwait(false);
        }
        finally {
            buffer.Release();
        }
    }

    private static async Task<Result<T>[]> CollectResultsConcurrently<T>(this IEnumerable<Task<T>> tasks, int concurrency)
    {
        var buffer = ArrayBuffer<Task<T>>.Lease(true, concurrency * 2);
        try {
            var runningCount = 0;
            var i = 0;
            foreach (var task in tasks) {
                buffer.Add(task);
                if (concurrency > runningCount)
                    runningCount++;
                else
                    await buffer[i++].SilentAwait(false);
            }
            return buffer.Count == 0
                ? Array.Empty<Result<T>>()
                : await ToResults(buffer.ToArray()).ConfigureAwait(false);
        }
        finally {
            buffer.Release();
        }
    }

    private static async Task<Result<T>[]> ToResults<T>(Task<T>[] tasks)
    {
        await Task.WhenAll(tasks).SilentAwait(false);
        var result = new Result<T>[tasks.Length];
        for (var i = 0; i < result.Length; i++)
            result[i] = tasks[i].ToResultSynchronously();
        return result;
    }
}
