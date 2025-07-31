using ActualLab.Concurrency;
using ActualLab.OS;
using ActualLab.Pooling;

namespace ActualLab.Locking;

#pragma warning disable CA2002

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
    private readonly ConcurrentPool<AsyncLock> _lockPool
        = new(() => new AsyncLock(reentryMode));

    public LockReentryMode ReentryMode { get; } = reentryMode;
    public int Count => _entries.Count;

    public AsyncLockSet(LockReentryMode reentryMode = LockReentryMode.Unchecked)
        : this(reentryMode, DefaultConcurrencyLevel, DefaultCapacity) { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public ValueTask<Releaser> Lock(TKey key, CancellationToken cancellationToken = default)
    {
        // This has to be non-async method, otherwise AsyncLocals
        // created inside it won't be available in caller's ExecutionContext.
        var (asyncLock, entry) = PrepareLock(key);
        try {
            var task = asyncLock.Lock(cancellationToken);
            return ToReleaserTask(entry, task);
        }
        catch {
            entry.EndUse();
            throw;
        }
    }

    // Private methods

    [MethodImpl(MethodImplOptions.NoInlining)]
    private (AsyncLock, Entry) PrepareLock(TKey key)
    {
        var spinWait = new SpinWait();
        while (true) {
            var entry = _entries.GetOrAdd(key, static (key1, self) => new Entry(self, key1), this);
            var asyncLock = entry.TryBeginUse();
            if (asyncLock is not null)
                return (asyncLock, entry);

            spinWait.SpinOnce(); // Safe for WASM
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async ValueTask<Releaser> ToReleaserTask(Entry entry, ValueTask<AsyncLock.Releaser> task)
    {
        try {
            var releaser = await task.ConfigureAwait(false);
            return new Releaser(entry, releaser);
        }
        catch {
            entry.EndUse();
            throw;
        }
    }

    // Nested types

    private sealed class Entry
    {
        private readonly AsyncLockSet<TKey> _owner;
        private readonly TKey _key;
        private readonly ResourceLease<AsyncLock> _lease;
        private volatile AsyncLock? _asyncLock;
        private int _useCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entry(AsyncLockSet<TKey> owner, TKey key)
        {
            _owner = owner;
            _key = key;
            _lease = owner._lockPool.Rent();
            _asyncLock = _lease.Resource;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AsyncLock? TryBeginUse()
        {
            lock (this) {
                var asyncLock = _asyncLock;
                if (asyncLock is not null)
                    ++_useCount;
                return asyncLock;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndUse()
        {
            var mustRelease = false;
            lock (this) {
                if (_asyncLock is not null && --_useCount == 0) {
                    _asyncLock = null;
                    mustRelease = true;
                }
            }
            if (mustRelease) {
                _owner._entries.TryRemove(_key, this);
                _lease.Dispose();
            }
        }
    }

    // Nested types

    public readonly struct Releaser(object entry, AsyncLock.Releaser releaser) : IAsyncLockReleaser
    {
        private readonly Entry? _entry = (Entry)entry;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkLockedLocally()
            => releaser.MarkLockedLocally();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            releaser.Dispose();
            _entry?.EndUse();
        }
    }
}
