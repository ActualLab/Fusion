using ActualLab.OS;
using ActualLab.Internal;

namespace ActualLab.Locking;

#pragma warning disable CA2002

/// <summary>
/// A keyed async lock set that allows locking on individual keys with optional reentry detection.
/// </summary>
public class AsyncLockSet<TKey>(
    LockReentryMode reentryMode,
    int concurrencyLevel,
    int capacity,
    IEqualityComparer<TKey>? equalityComparer = null
    ) where TKey : notnull
{
    public static int DefaultConcurrencyLevel => HardwareInfo.ProcessorCountPo2;
    public static int DefaultCapacity => HardwareInfo.GetProcessorCountPo2Factor(4).Clamp(32, 256);

    private readonly ConcurrentDictionary<TKey, Entry> _entries
        = new(concurrencyLevel, capacity, equalityComparer ?? EqualityComparer<TKey>.Default);

    public LockReentryMode ReentryMode { get; } = reentryMode;
    public int Count => _entries.Count;

    public AsyncLockSet(LockReentryMode reentryMode = LockReentryMode.Unchecked)
        : this(reentryMode, DefaultConcurrencyLevel, DefaultCapacity) { }

    public ValueTask<Releaser> Lock(TKey key, CancellationToken cancellationToken = default)
    {
        // This has to be a non-async method, otherwise AsyncLocals
        // created inside it won't be available in the caller's ExecutionContext.
        Entry entry;
        var spinWait = new SpinWait();
        while (true) {
            entry = _entries.GetOrAdd(key, static (key, self) => new Entry(self, key), this);
            if (entry.TryBeginUse())
                break;

            spinWait.SpinOnce(); // Safe for WASM
        }
        try {
            if (entry.IsLockedLocally)
                return ReentryMode == LockReentryMode.CheckedFail
                    ? throw Errors.AlreadyLocked()
                    : new ValueTask<Releaser>(new Releaser(entry, isLocked: false));

            var task = entry.Semaphore.WaitAsync(cancellationToken);
            return task.IsCompletedSuccessfully
                ? new ValueTask<Releaser>(new Releaser(entry))
                : CompleteAsync(task, entry);
        }
        catch {
            entry.EndUse();
            throw;
        }

        static async ValueTask<Releaser> CompleteAsync(Task task, Entry entry) {
            try {
                await task.ConfigureAwait(false);
                return new Releaser(entry);
            }
            catch {
                entry.EndUse();
                throw;
            }
        }
    }

    // Nested types

    /// <summary>
    /// Releases the keyed lock in an <see cref="AsyncLockSet{TKey}"/> when disposed.
    /// </summary>
    public struct Releaser : IAsyncLockReleaser
    {
        private readonly Entry _entry;
        private readonly bool _isLocked;
        private bool _unmarkOnRelease;

        internal Releaser(Entry entry)
        {
            _entry = entry;
            _isLocked = true;
        }

        internal Releaser(Entry entry, bool isLocked)
        {
            _entry = entry;
            _isLocked = isLocked;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkLockedLocally(bool unmarkOnRelease = true)
        {
            if (!_isLocked)
                return;

            _unmarkOnRelease |= unmarkOnRelease;
            _entry.IsLockedLocally = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_isLocked) {
                if (_unmarkOnRelease)
                    _entry.LockedLocallyTag?.Value = null;
                _entry.Release();
            }
            else {
                _entry?.EndUse();
            }
        }
    }

    /// <summary>
    /// Internal entry that holds the semaphore and reentry state for a single key.
    /// </summary>
    // Mimics IAsyncLock interface
    internal sealed class Entry(AsyncLockSet<TKey> owner, TKey key)
    {
        private int _useCount;

        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public AsyncLocal<object?>? LockedLocallyTag { get; }
            = owner.ReentryMode == LockReentryMode.Unchecked ? null : new AsyncLocal<object?>();

        public bool IsLockedLocally {
            get => LockedLocallyTag?.Value is not null;
            set => LockedLocallyTag?.Value = value ? AsyncLock.LockedLocallyTag.Instance : null;
        }

        public bool TryBeginUse()
        {
            while (true) {
                var useCount = Volatile.Read(ref _useCount);
                if (useCount < 0)
                    return false; // Already closed
                if (Interlocked.CompareExchange(ref _useCount, useCount + 1, useCount) == useCount)
                    return true;
            }
        }

        public void EndUse()
        {
            while (true) {
                var useCount = Volatile.Read(ref _useCount);
                if (useCount <= 0) {
                    if (useCount == 0)
                        throw Errors.InternalError("AsyncLockSet.Entry is in a broken state.");
                    return; // Already closed
                }
                var nextUseCount = useCount == 1 ? -1 : useCount - 1;
                if (Interlocked.CompareExchange(ref _useCount, nextUseCount, useCount) != useCount)
                    continue;
                if (nextUseCount >= 0)
                    return;

                Close();
                return;
            }
        }

        public void Release()
        {
            while (true) {
                var useCount = Volatile.Read(ref _useCount);
                if (useCount <= 0) {
                    if (useCount == 0)
                        throw Errors.InternalError("AsyncLockSet.Entry is in a broken state.");
                    return; // Already closed
                }
                if (useCount > 1)
                    break;
                if (Interlocked.CompareExchange(ref _useCount, -1, 1) == 1) {
                    Close();
                    return;
                }
            }

            try {
                Semaphore.Release();
            }
            finally {
                EndUse();
            }
        }

        private void Close()
        {
            if (owner._entries.TryRemove(key, this))
                Semaphore.Dispose();
        }
    }
}
