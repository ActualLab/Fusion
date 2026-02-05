namespace ActualLab.Locking;

/// <summary>
/// Extension methods for <see cref="SemaphoreSlim"/>.
/// </summary>
public static class SemaphoreSlimExt
{
    public static ValueTask<ClosedDisposable<SemaphoreSlim>> Lock(
        this SemaphoreSlim semaphore,
        CancellationToken cancellationToken = default)
    {
        var task = semaphore.WaitAsync(cancellationToken);
        return task.IsCompletedSuccessfully
            ? ValueTaskExt.FromResult(new ClosedDisposable<SemaphoreSlim>(semaphore, static x => x.Release()))
            : CompleteAsync(task, semaphore);

        static async ValueTask<ClosedDisposable<SemaphoreSlim>> CompleteAsync(Task task, SemaphoreSlim semaphore) {
            await task.ConfigureAwait(false);
            return new ClosedDisposable<SemaphoreSlim>(semaphore, static x => x.Release());
        }
    }
}
