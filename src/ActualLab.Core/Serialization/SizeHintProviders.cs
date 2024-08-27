using ActualLab.Internal;

namespace ActualLab.Serialization;

public static class SizeHintProviders
{
    private static readonly ConcurrentDictionary<Type, Delegate> Registry = new();

    public static readonly int ArrayExtra = 8;

    static SizeHintProviders()
    {
        Register<byte[]>(x => ArrayExtra + x.Length);
        Register<string>(x => ArrayExtra + (2 * x.Length));
        Register<ReadOnlyMemory<byte>>(x => ArrayExtra + x.Length);
        Register<ReadOnlyMemory<char>>(x => ArrayExtra + (2 * x.Length));
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
