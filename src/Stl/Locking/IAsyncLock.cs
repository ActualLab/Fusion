namespace ActualLab.Locking;

public interface IAsyncLock
{
    LockReentryMode ReentryMode { get; }
    ValueTask<IAsyncLockReleaser> Lock(CancellationToken cancellationToken = default);
}

public interface IAsyncLock<TReleaser> : IAsyncLock
    where TReleaser : IAsyncLockReleaser
{
    new ValueTask<TReleaser> Lock(CancellationToken cancellationToken = default);
}

public interface IAsyncLockReleaser : IDisposable
{
    void MarkLockedLocally();
}
