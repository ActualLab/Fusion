namespace ActualLab.Trimming;

/// <summary>
/// A <see cref="CodeKeeper"/> that retains types by creating uninitialized instances
/// to prevent trimming.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public class TypeCodeKeeper : CodeKeeper
{
    public virtual T KeepType<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        bool ensureInitialized = false)
        => ensureInitialized || AlwaysFalse
            ? KeepTypeImpl<T>()
            : default!;

    // Private methods

    private static T KeepTypeImpl<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
        => TypeKeeper<T>.Instance;

    // Nested types

    /// <summary>
    /// Holds a singleton uninitialized instance of the given type to retain it during trimming.
    /// </summary>
    private static class TypeKeeper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    {
        public static readonly T Instance;

        static TypeKeeper()
        {
            if (typeof(T).IsArray || typeof(T) == typeof(string)) {
                Instance = default!;
                return;
            }

            try {
#if !NETSTANDARD2_0
                Instance = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
#else
                Instance = (T)FormatterServices.GetUninitializedObject(typeof(T));
#endif
            }
            catch {
                Instance = default!;
            }
            try {
                _ = Instance?.GetHashCode();
            }
            catch {
                // Intended
            }
        }
    }
}
