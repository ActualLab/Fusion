namespace ActualLab.Compliance;

// Thread-static rather than AsyncLocal: it's read on every ToString() of every sanitized value.
// So a scope doesn't flow across an await - don't wrap awaited work in one.

/// <summary>
/// Controls whether <see cref="ISanitized"/> values render their masked form. Inactive unless a
/// scope turns it on, which is what <see cref="SanitizingLogger"/> does around each log call.
/// </summary>
/// <remarks>
/// Off by default on purpose: a masking member is usually written as
/// <c>get =&gt; Sanitizer.MaybeSanitize&lt;T&gt;(field)</c>, and a serializer reads that same getter -
/// so were masking the default, the masked form would be what reaches the wire.
/// </remarks>
public static class Sanitization
{
    [ThreadStatic] private static bool _isActive;

    public static bool IsActive {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _isActive;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Scope Begin()
        => new(true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Scope Suspend()
        => new(false);

    // Nested types

    public readonly struct Scope : IDisposable
    {
        private readonly bool _oldIsActive;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Scope(bool isActive)
        {
            _oldIsActive = _isActive;
            _isActive = isActive;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
            => _isActive = _oldIsActive;
    }
}
