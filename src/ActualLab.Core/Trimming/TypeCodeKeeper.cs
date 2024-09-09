using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Trimming;

public class TypeCodeKeeper : CodeKeeper
{
    public virtual T KeepType<T>(bool ensureInitialized = false)
        => ensureInitialized || AlwaysFalse
            ? KeepTypeImpl<T>()
            : default!;

    // Private methods

    private static T KeepTypeImpl<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
        => TypeKeeper<T>.Instance;

    // Nested types

    private static class TypeKeeper<T>
    {
        public static readonly T Instance;

        static TypeKeeper()
        {
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
