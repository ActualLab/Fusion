namespace ActualLab.Compliance;

/// <summary>
/// Maps a raw string to the form that may be rendered while <see cref="Sanitization"/> is active.
/// A type rather than a delegate, so it can be the type argument of
/// <see cref="SanitizedString{TSanitizer}"/>. Implementations must be stateless and thread-safe.
/// </summary>
public abstract class Sanitizer
{
    private static readonly ConcurrentDictionary<Type, Sanitizer> Cache = new();

    // Instance methods

    public abstract string Apply(string value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string MaybeApply(string value)
        => Sanitization.IsActive ? Apply(value) : value;

    // (Maybe)Sanitize

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Sanitize<TSanitizer>(string value)
        where TSanitizer : Sanitizer, new()
        => Get<TSanitizer>().Apply(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string MaybeSanitize<TSanitizer>(string value)
        where TSanitizer : Sanitizer, new()
        => Sanitization.IsActive ? Get<TSanitizer>().Apply(value) : value;

    // Get

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TSanitizer Get<TSanitizer>()
        where TSanitizer : Sanitizer, new()
        => Instance<TSanitizer>.Value;

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume sanitizer code is preserved")]
    public static Sanitizer Get(Type sanitizerType)
        => Cache.GetOrAdd(sanitizerType, static t => {
            if (!typeof(Sanitizer).IsAssignableFrom(t))
                throw new ArgumentOutOfRangeException(nameof(sanitizerType));

            return (Sanitizer)t.CreateInstance();
        });

    // Nested types

    private static class Instance<TSanitizer>
        where TSanitizer : Sanitizer, new()
    {
        // Seeded through Get(Type), so both overloads hand out the same instance
        public static readonly TSanitizer Value = (TSanitizer)Get(typeof(TSanitizer));
    }
}
