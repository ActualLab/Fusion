namespace ActualLab.Async;

public static partial class TaskExt
{
    // SuppressExceptions

    // The continuations of parameterless SuppressExceptions overloads read t.Exception
    // to mark the error as observed, see SilentTaskAwaiter.GetResult
    public static Task SuppressExceptions(this Task task)
        => task.IsCompletedSuccessfully
            ? task
            : task.ContinueWith(
                static t => { _ = t.Exception; },
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    public static async Task SuppressExceptions(this Task task, Func<Exception, bool>? filter)
    {
        try {
            await task.ConfigureAwait(false);
        }
        catch (Exception e) {
            if (filter?.Invoke(e) ?? true)
                return;
            throw;
        }
    }

    public static Task<T> SuppressExceptions<T>(this Task<T> task)
        => task.IsCompletedSuccessfully
            ? task
            : task.ContinueWith(
                static t => {
                    if (t.IsCompletedSuccessfully)
                        return t.GetAwaiter().GetResult();

                    _ = t.Exception;
                    return default!;
                },
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    public static async Task<T> SuppressExceptions<T>(this Task<T> task, Func<Exception, bool>? filter)
    {
        try {
            return await task.ConfigureAwait(false);
        }
        catch (Exception e) {
            if (filter?.Invoke(e) ?? true)
                return default!;
            throw;
        }
    }

    // SuppressCancellation

    public static Task SuppressCancellation(this Task task)
        => task.ContinueWith(
            static t => {
                if (t.IsCompletedSuccessfully || t.IsCanceledOrFaultedWithOce())
                    return;

                t.GetAwaiter().GetResult();
            },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    public static Task<T> SuppressCancellation<T>(this Task<T> task)
        => task.ContinueWith(
            static t => t.IsCanceledOrFaultedWithOce()
                ? default!
                : t.GetAwaiter().GetResult(),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
