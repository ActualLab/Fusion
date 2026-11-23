namespace ActualLab.Compliance;

/// <summary>
/// Maps a raw string to the form that may be rendered while <see cref="Sanitization"/> is active.
/// A type rather than a delegate, so it can be the type argument of
/// <see cref="SanitizedString{TSanitizer}"/>. Implementations must be stateless and thread-safe.
/// </summary>
public abstract class Sanitizer
{
    private static readonly ConcurrentDictionary<Type, Sanitizer> Cache = new();

    // Static (Maybe)Sanitize

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(value))]
    public static string? Sanitize<TSanitizer>(string? value)
        where TSanitizer : Sanitizer
        => Get<TSanitizer>().Apply(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(value))]
    public static string? MaybeSanitize<TSanitizer>(string? value)
        where TSanitizer : Sanitizer
        => Sanitization.IsActive ? Get<TSanitizer>().Apply(value) : value;

    // Static Get

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TSanitizer Get<TSanitizer>()
        where TSanitizer : Sanitizer
        => Instance<TSanitizer>.Value;

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume sanitizer code is preserved")]
    public static Sanitizer Get(Type sanitizerType)
        => Cache.GetOrAdd(sanitizerType, static t => {
            if (!typeof(Sanitizer).IsAssignableFrom(t))
                throw new ArgumentOutOfRangeException(nameof(sanitizerType));

            return (Sanitizer)t.CreateInstance();
        });

    // Instance methods

    // Null is passed through rather than masked: there's nothing to reveal, and a "<<hidden>>"
    // standing in for an absent value would misreport what the object actually holds.
    // Filtering it here means no implementation has to remember to.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(value))]
    public string? Apply(string? value)
        => value is null ? null : Sanitize(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(value))]
    public string? MaybeApply(string? value)
        => Sanitization.IsActive ? Apply(value) : value;

    // Protected methods

    protected abstract string Sanitize(string value);

    // Nested types

    private static class Instance<TSanitizer>
        where TSanitizer : Sanitizer
    {
        // Shares Get(Type)'s cache, so both overloads hand out the same instance. Activator.CreateInstance<T>
        // is what "new TSanitizer()" compiles to: NativeAOT resolves it for each closed TSanitizer
        public static readonly TSanitizer Value
            = (TSanitizer)Cache.GetOrAdd(typeof(TSanitizer), static _ => CreateInstance());

        [UnconditionalSuppressMessage("Trimming", "IL2091", Justification = "Resolved for each closed TSanitizer")]
        private static TSanitizer CreateInstance()
            => Activator.CreateInstance<TSanitizer>();
    }
}
