using ActualLab.Locking;
using ActualLab.OS;

namespace ActualLab.Fusion;

/// <summary>
/// A keyed stash that lets an update operation hand a freshly produced value
/// to a compute method that is about to recompute it for the same key.
/// A per-key <see cref="AsyncLockSet{TKey}"/> serializes updates for the same key.
/// </summary>
public sealed class ComputeMethodResultStash<TKey, TValue>
    where TKey : notnull
{
    private const int StateEmpty = 0;
    private const int StateStashed = 1;
    private const int StateConsumed = 2;

    private readonly ConcurrentDictionary<TKey, Reservation> _reservations;

    public AsyncLockSet<TKey> Locks { get; }
    public int Count => _reservations.Count;

    public ComputeMethodResultStash(
        LockReentryMode reentryMode = LockReentryMode.CheckedFail,
        IEqualityComparer<TKey>? keyComparer = null)
        : this(
            new AsyncLockSet<TKey>(
                reentryMode,
                AsyncLockSet<TKey>.DefaultConcurrencyLevel,
                AsyncLockSet<TKey>.DefaultCapacity,
                keyComparer),
            keyComparer)
    { }

    public ComputeMethodResultStash(AsyncLockSet<TKey> locks, IEqualityComparer<TKey>? keyComparer = null)
    {
        Locks = locks;
        _reservations = new ConcurrentDictionary<TKey, Reservation>(
            HardwareInfo.ProcessorCountPo2,
            HardwareInfo.GetProcessorCountPo2Factor(4).Clamp(32, 256),
            keyComparer ?? EqualityComparer<TKey>.Default);
    }

    /// <summary>
    /// Acquires the per-key lock for <paramref name="key"/> and registers an empty slot.
    /// The returned <see cref="Reservation"/>'s <see cref="IDisposable.Dispose"/> removes the slot
    /// (if still owned by this reservation) and releases the lock.
    /// </summary>
    public async ValueTask<Reservation> LockAndReserve(TKey key, CancellationToken cancellationToken = default)
    {
        var releaser = await Locks.Lock(key, cancellationToken).ConfigureAwait(false);
        try {
            var reservation = new Reservation(this, key, releaser);
            _reservations[key] = reservation;
            return reservation;
        }
        catch {
            releaser.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Tries to pull a stashed value for <paramref name="key"/>. On success the reservation
    /// is removed from the stash so subsequent calls return <c>false</c>.
    /// </summary>
    public bool TryUnstash(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (!_reservations.TryGetValue(key, out var reservation)) {
            value = default;
            return false;
        }
        if (!reservation.TryConsume(out value))
            return false;

        _reservations.TryRemove(key, reservation);
        return true;
    }

    // Nested types

    /// <summary>
    /// A single stash slot reserved via <see cref="LockAndReserve"/>. Holds the per-key lock
    /// until disposed; also holds the stashed value (if any).
    /// </summary>
    public sealed class Reservation : IDisposable
    {
        private readonly AsyncLockSet<TKey>.Releaser _releaser;
        private int _state;
        private int _disposed;
        private TValue? _value;

        public ComputeMethodResultStash<TKey, TValue> Owner { get; }
        public TKey Key { get; }
        public bool IsStashed => Volatile.Read(ref _state) == StateStashed;

        internal Reservation(ComputeMethodResultStash<TKey, TValue> owner, TKey key, AsyncLockSet<TKey>.Releaser releaser)
        {
            Owner = owner;
            Key = key;
            _releaser = releaser;
        }

        /// <summary>
        /// Stashes <paramref name="value"/> so a subsequent <see cref="ComputeMethodResultStash{TKey,TValue}.TryUnstash"/>
        /// for the same key returns it. Can be called at most once per reservation.
        /// </summary>
        public void Stash(TValue value)
        {
            _value = value;
            // Interlocked.CompareExchange issues a full fence, so _value is visible
            // to any thread that subsequently observes StateStashed.
            var prev = Interlocked.CompareExchange(ref _state, StateStashed, StateEmpty);
            if (prev != StateEmpty) {
                _value = default;
                throw prev == StateStashed
                    ? new InvalidOperationException("The value is already stashed.")
                    : new InvalidOperationException("The reservation is already consumed or disposed.");
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try {
                Interlocked.Exchange(ref _state, StateConsumed);
                _value = default;
                Owner._reservations.TryRemove(Key, this);
            }
            finally {
                _releaser.Dispose();
            }
        }

        internal bool TryConsume([MaybeNullWhen(false)] out TValue value)
        {
            if (Interlocked.CompareExchange(ref _state, StateConsumed, StateStashed) != StateStashed) {
                value = default;
                return false;
            }
            value = _value!;
            _value = default;
            return true;
        }
    }
}
