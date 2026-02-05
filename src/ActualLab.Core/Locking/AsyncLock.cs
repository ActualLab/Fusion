using ActualLab.Internal;

namespace ActualLab.Locking;

/// <summary>
/// A semaphore-based async lock with optional reentry detection.
/// </summary>
public sealed class AsyncLock(LockReentryMode reentryMode = LockReentryMode.Unchecked)
    : IAsyncLock<AsyncLock.Releaser>
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly AsyncLocal<object?>? _lockedLocallyTag
        = reentryMode == LockReentryMode.Unchecked ? null : new AsyncLocal<object?>();

    public LockReentryMode ReentryMode { get; } = reentryMode;

    public bool IsLockedLocally {
        get => _lockedLocallyTag?.Value is not null;
        internal set => _lockedLocallyTag?.Value = value ? LockedLocallyTag.Instance : null;
    }

    public void Dispose()
        => _semaphore.Dispose();

    async ValueTask<IAsyncLockReleaser> IAsyncLock.Lock(CancellationToken cancellationToken)
        => await Lock(cancellationToken).ConfigureAwait(false);
    public ValueTask<Releaser> Lock(CancellationToken cancellationToken = default)
    {
        if (IsLockedLocally)
            return ReentryMode == LockReentryMode.CheckedFail
                ? throw Errors.AlreadyLocked()
                : default;

        var task = _semaphore.WaitAsync(cancellationToken);
        return task.IsCompletedSuccessfully
            ? new ValueTask<Releaser>(new Releaser(this))
            : CompleteAsync(task, this);

        static async ValueTask<Releaser> CompleteAsync(Task task, AsyncLock asyncLock) {
            await task.ConfigureAwait(false);
            return new Releaser(asyncLock);
        }
    }

    // Nested types

    /// <summary>
    /// Sentinel object stored in <see cref="AsyncLocal{T}"/> to indicate a lock is held.
    /// </summary>
    public sealed class LockedLocallyTag
    {
        public static readonly LockedLocallyTag Instance = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LockedLocallyTag() { }
    }

    /// <summary>
    /// Releases the <see cref="AsyncLock"/> when disposed.
    /// </summary>
    public struct Releaser : IAsyncLockReleaser
    {
        private readonly AsyncLock? _asyncLock;
        private bool _unmarkOnRelease;

        internal Releaser(AsyncLock asyncLock)
            => _asyncLock = asyncLock;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkLockedLocally(bool unmarkOnRelease = true)
        {
            _unmarkOnRelease |= unmarkOnRelease;
            _asyncLock?.IsLockedLocally = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_asyncLock is null)
                return;

            if (_unmarkOnRelease)
                _asyncLock._lockedLocallyTag?.Value = null;
            _asyncLock._semaphore.Release();
        }
    }
}
