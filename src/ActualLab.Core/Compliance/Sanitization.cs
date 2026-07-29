namespace ActualLab.Compliance;

// Thread-static rather than AsyncLocal: it's read on every ToString() of every sanitized value.
// So a scope doesn't flow across an await - don't wrap awaited work in one.

/// <summary>
/// Controls whether <see cref="ISanitized"/> values render their masked form.
/// Mirrors <c>Invalidation</c> in Fusion: an ambient flag plus scopes that set it.
/// </summary>
public static class Sanitization
{
    [ThreadStatic] private static State _state;

    // What IsActive falls back to when no scope is in effect
    public static bool IsAlwaysActive { get; set; } = true;

    public static bool IsActive {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state switch {
            State.Active => true,
            State.Suspended => false,
            _ => IsAlwaysActive,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Scope Begin()
        => new(State.Active);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Scope Suspend()
    {
        // Overrides IsAlwaysActive, which is what lets a test read a value it just wrote
        return new Scope(State.Suspended);
    }

    // Nested types

    internal enum State : byte
    {
        Unset = 0,
        Active,
        Suspended,
    }

    public readonly struct Scope : IDisposable
    {
        private readonly State _oldState;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Scope(State state)
        {
            _oldState = _state;
            _state = state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
            => _state = _oldState;
    }
}
