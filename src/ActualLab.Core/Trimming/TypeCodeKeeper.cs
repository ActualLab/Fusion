using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Trimming;

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
