namespace ActualLab.Async;

/// <summary>
/// Extension methods for <see cref="SemaphoreSlim"/>.
/// </summary>
public static class SemaphoreSlimExt
{
    public static void ReleaseSilently(this SemaphoreSlim semaphore, int releaseCount = 1)
    {
        try {
            semaphore.Release(releaseCount);
        }
        catch {
            // Intended
        }
    }
}
