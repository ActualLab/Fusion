using ActualLab.Internal;

namespace ActualLab.Serialization;

public static class SizeHintProviders
{
    private static readonly ConcurrentDictionary<Type, Delegate> Registry = new();

    public static readonly int ArrayExtra = 8; // Size in bytes

    static SizeHintProviders()
    {
        Register<byte[]?>(static x => ArrayExtra + x?.Length ?? 0);
        Register<string?>(static x => ArrayExtra + (2 * x?.Length ?? 0));
        Register<ReadOnlyMemory<byte>?>(static x => ArrayExtra + x?.Length ?? 0);
        Register<ReadOnlyMemory<char>?>(static x => ArrayExtra + (2 * x?.Length ?? 0));
    }

    public static void Register<T>(Func<T, int> provider)
    {
        if (!Registry.TryAdd(typeof(T), provider))
            throw Errors.ProviderAlreadyRegistered<T>();
    }

    public static bool Unregister<T>(Func<T, int> provider)
        => Registry.TryRemove(typeof(T), out _);

    public static Func<T, int>? Get<T>()
        => Registry.TryGetValue(typeof(T), out var func)
            ? (Func<T, int>)func
            : null;
}
