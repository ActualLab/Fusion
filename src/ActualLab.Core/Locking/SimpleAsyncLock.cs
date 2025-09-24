namespace ActualLab.Locking;

public sealed class SimpleAsyncLock : IAsyncLock<SimpleAsyncLock.Releaser>
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public LockReentryMode ReentryMode => LockReentryMode.Unchecked;

    public void Dispose()
        => _semaphore.Dispose();

    async ValueTask<IAsyncLockReleaser> IAsyncLock.Lock(CancellationToken cancellationToken)
        => await Lock(cancellationToken).ConfigureAwait(false);
    public ValueTask<Releaser> Lock(CancellationToken cancellationToken = default)
    {
        var task = _semaphore.WaitAsync(cancellationToken);
        return Releaser.NewWhenCompleted(task, this);
    }

    // Nested types

    public readonly struct Releaser(SimpleAsyncLock asyncLock) : IAsyncLockReleaser
    {
        internal static ValueTask<Releaser> NewWhenCompleted(Task task, SimpleAsyncLock asyncLock)
        {
            return task.IsCompletedSuccessfully()
                ? ValueTaskExt.FromResult(new Releaser(asyncLock))
                : CompleteAsynchronously(task, asyncLock);

            static async ValueTask<Releaser> CompleteAsynchronously(Task task1, SimpleAsyncLock asyncLock1)
            {
                await task1.ConfigureAwait(false);
                return new Releaser(asyncLock1);
            }
        }

        public void MarkLockedLocally(bool markOnRelease = true)
        { }

        public void Dispose()
            => asyncLock._semaphore.Release();
    }
}
