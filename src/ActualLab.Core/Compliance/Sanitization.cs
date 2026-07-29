namespace ActualLab.Compliance;

// Thread-static rather than AsyncLocal: it's read on every ToString() of every sanitized value.
// So a scope doesn't flow across an await - don't wrap awaited work in one.

/// <summary>
/// Controls whether <see cref="ISanitized"/> values render their masked form. Active unless
/// suspended, so a value is masked by default; mirrors <c>Invalidation</c> in Fusion in that it's
/// an ambient flag plus scopes that set it.
/// </summary>
public static class Sanitization
{
    [ThreadStatic] private static bool _isSuspended;
    // Volatile, so a write from one thread is published to the readers on all the others -
    // and a plain read on this path is as cheap as a non-volatile one on x64
    private static volatile bool _isGloballySuspended;

    // A debugging escape hatch: suspends sanitization process-wide, whatever the scopes say
    public static bool IsGloballySuspended {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _isGloballySuspended;
        set => _isGloballySuspended = value;
    }

    public static bool IsSuspended {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _isGloballySuspended || _isSuspended;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Scope Suspend()
        => new(true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Scope Resume()
    {
        // Doesn't override IsGloballySuspended - that one is meant to win everywhere
        return new Scope(false);
    }

    // Nested types

    public readonly struct Scope : IDisposable
    {
        private readonly bool _oldIsSuspended;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Scope(bool isSuspended)
        {
            _oldIsSuspended = _isSuspended;
            _isSuspended = isSuspended;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
            => _isSuspended = _oldIsSuspended;
    }
}
