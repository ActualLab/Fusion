namespace ActualLab.Locking;

public interface IAsyncLock
{
    public LockReentryMode ReentryMode { get; }
    public ValueTask<IAsyncLockReleaser> Lock(CancellationToken cancellationToken = default);
}

public interface IAsyncLock<TReleaser> : IAsyncLock
    where TReleaser : IAsyncLockReleaser
{
    public new ValueTask<TReleaser> Lock(CancellationToken cancellationToken = default);
}

public interface IAsyncLockReleaser : IDisposable
{
    public void MarkLockedLocally();
}
