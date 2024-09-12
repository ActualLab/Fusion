namespace ActualLab.Generators;

public static class RandomShared
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Next()
    {
#if NET6_0_OR_GREATER
        return Random.Shared.Next();
#else
        return ThreadRandom.Instance.Next();
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double NextDouble()
    {
#if NET6_0_OR_GREATER
        return Random.Shared.NextDouble();
#else
        return ThreadRandom.Instance.NextDouble();
#endif
    }
}

#if !NET6_0_OR_GREATER

static file class ThreadRandom
{
    private static readonly Random SharedInstance = new();
    [ThreadStatic] private static Random? _instance;

    public static Random Instance {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _instance ??= CreateInstance();
    }

    public static int Next() => Instance.Next();
    public static double NextDouble() => Instance.NextDouble();

    // Private methods

    private static Random CreateInstance()
    {
        lock (SharedInstance)
            return new(SharedInstance.Next() + Environment.CurrentManagedThreadId);
    }
}

#endif
