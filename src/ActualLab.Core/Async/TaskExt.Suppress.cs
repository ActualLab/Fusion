using System.Runtime.ExceptionServices;

namespace ActualLab.Async;

public static partial class TaskExt
{
    public static async Task SuppressExceptions(this Task task, Func<Exception, bool>? filter = null)
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

    public static async Task<T> SuppressExceptions<T>(this Task<T> task, Func<Exception, bool>? filter = null)
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

    public static Task SuppressCancellation(this Task task)
        => task.ContinueWith(
            static t => {
                if (t.IsCompletedSuccessfully() || t.IsCanceledOrFaultedWithOce())
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
