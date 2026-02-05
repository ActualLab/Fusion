namespace ActualLab.Locking;

/// <summary>
/// Defines the contract for an asynchronous lock that supports cancellation.
/// </summary>
public interface IAsyncLock : IDisposable
{
    public LockReentryMode ReentryMode { get; }
    public ValueTask<IAsyncLockReleaser> Lock(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the contract for an asynchronous lock with a strongly-typed releaser.
/// </summary>
public interface IAsyncLock<TReleaser> : IAsyncLock
    where TReleaser : IAsyncLockReleaser
{
    public new ValueTask<TReleaser> Lock(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the contract for releasing an acquired async lock.
/// </summary>
public interface IAsyncLockReleaser : IDisposable
{
    public void MarkLockedLocally(bool unmarkOnRelease = true);
}
