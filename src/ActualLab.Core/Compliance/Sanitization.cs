namespace ActualLab.Compliance;

// Thread-static rather than AsyncLocal: it's read on every ToString() of every sanitized value.
// So a scope doesn't flow across an await - don't wrap awaited work in one.

/// <summary>
/// Controls whether <see cref="ISanitized"/> values render their masked form. Suspended by
/// default, so a value renders in full unless something turns sanitization on for the current
/// thread - which is what <see cref="SanitizingLogger"/> does around each log call.
/// </summary>
public static class Sanitization
{
    // Null means "follow IsGloballySuspended"; a scope sets it either way and restores it on exit
    [ThreadStatic] private static bool? _isSuspendedOverride;
    // Read/written with Volatile, so a write from one thread reaches the readers on all the others -
    // and on x64 the acquire read costs the same as a plain one
    private static bool _isGloballySuspended = true;

    /// <summary>
    /// The process-wide default, suspended unless changed. A masking getter such as
    /// <c>get =&gt; Sanitizer.MaybeSanitize&lt;T&gt;(field)</c> is read by serializers too, so
    /// masking must stay off outside an explicit scope or it would reach the wire.
    /// </summary>
    public static bool IsGloballySuspended {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _isGloballySuspended);
        set => Volatile.Write(ref _isGloballySuspended, value);
    }

    // A thread's scope wins over the global default, so Resume() works even when suspended
    // process-wide - otherwise nothing could turn masking on for just the log call
    public static bool IsSuspended {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _isSuspendedOverride ?? Volatile.Read(ref _isGloballySuspended);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Scope Suspend()
        => new(true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Scope Begin()
        => new(false);

    // Nested types

    public readonly struct Scope : IDisposable
    {
        private readonly bool? _oldIsSuspendedOverride;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Scope(bool isSuspended)
        {
            _oldIsSuspendedOverride = _isSuspendedOverride;
            _isSuspendedOverride = isSuspended;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
            => _isSuspendedOverride = _oldIsSuspendedOverride;
    }
}
