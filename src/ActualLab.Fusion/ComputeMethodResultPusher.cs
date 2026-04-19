using ActualLab.Locking;

namespace ActualLab.Fusion;

/// <summary>
/// A keyed stash that lets an update operation hand a freshly produced value
/// to a compute method that is about to recompute it for the same key.
/// A per-key <see cref="AsyncLockSet{TKey}"/> serializes updates for the same key.
/// </summary>
public sealed class ComputeMethodResultPusher<TKey, TValue>(
    Func<TKey, CancellationToken, Task<TValue>> caller,
    AsyncLockSet<TKey> locks,
    IEqualityComparer<TKey>? keyComparer = null)
    where TKey : notnull
{
    private const int StateEmpty = 0;
    private const int StatePushing = 1;
    private const int StateDisposed = 2;

    public Func<TKey, CancellationToken, Task<TValue>> Caller { get; init; } = caller;
    public ConcurrentDictionary<TKey, Reservation> Reservations { get; init; }
        = new(keyComparer ?? EqualityComparer<TKey>.Default);
    public AsyncLockSet<TKey> Locks { get; init; } = locks;

    public ComputeMethodResultPusher(
        Func<TKey, CancellationToken, Task<TValue>> caller,
        LockReentryMode reentryMode = LockReentryMode.CheckedFail,
        IEqualityComparer<TKey>? keyComparer = null)
        : this(
            caller,
            new AsyncLockSet<TKey>(
                reentryMode,
                AsyncLockSet<TKey>.DefaultConcurrencyLevel,
                AsyncLockSet<TKey>.DefaultCapacity,
                keyComparer),
            keyComparer)
    { }

    public async ValueTask<Reservation> LockAndReserve(TKey key, CancellationToken cancellationToken = default)
    {
        var releaser = await Locks.Lock(key, cancellationToken).ConfigureAwait(false);
        try {
            var reservation = new Reservation(this, key, releaser);
            Reservations[key] = reservation;
            return reservation;
        }
        catch {
            releaser.Dispose();
            throw;
        }
    }

    public bool TryPull(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (Reservations.TryGetValue(key, out var reservation))
            return reservation.TryPull(out value);

        value = default;
        return false;
    }

    // Nested types

    /// <summary>
    /// A single stash slot reserved via <see cref="LockAndReserve"/>. Holds the per-key lock
    /// until disposed; also holds the stashed value (if any).
    /// </summary>
    public sealed class Reservation : IDisposable
    {
        private AsyncLockSet<TKey>.Releaser _releaser;
        private int _state;
        private TValue? _value;

        public ComputeMethodResultPusher<TKey, TValue> Owner { get; }
        public TKey Key { get; }
        public bool IsStashed => Volatile.Read(ref _state) == StatePushing;

        internal Reservation(ComputeMethodResultPusher<TKey, TValue> owner, TKey key, AsyncLockSet<TKey>.Releaser releaser)
        {
            Owner = owner;
            Key = key;
            _releaser = releaser;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _state, StateDisposed) == StateDisposed)
                return;

            _value = default;
            Owner.Reservations.TryRemove(Key, this);
            _releaser.Dispose();
        }

        public async ValueTask Push(TValue value, CancellationToken cancellationToken = default)
        {
            _value = value;
            if (Interlocked.CompareExchange(ref _state, StatePushing, StateEmpty) == StateDisposed) {
                _value = default;
                throw new ObjectDisposedException(nameof(Reservation));
            }

            // Invalidate existing value
            using (Invalidation.Begin())
                _ = Owner.Caller.Invoke(Key, default);

            // We assume the caller will try to pull the value during this call
            await Owner.Caller.Invoke(Key, cancellationToken).ConfigureAwait(false);
        }

        internal bool TryPull([MaybeNullWhen(false)] out TValue value)
        {
            if (Interlocked.CompareExchange(ref _state, StateEmpty, StatePushing) != StatePushing) {
                value = default;
                return false;
            }

            value = _value!;
            _value = default;
            return true;
        }
    }
}
