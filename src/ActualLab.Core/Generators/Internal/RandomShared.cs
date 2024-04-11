namespace ActualLab.Generators.Internal;

public static class RandomShared
{
#if !NET6_0_OR_GREATER
    private static readonly Random Random = new Random();
#endif

#if NET6_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static int Next()
    {
#if NET6_0_OR_GREATER
        return Random.Shared.Next();
#else
        lock (Random)
            return Random.Next();
#endif
    }

#if NET6_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static double NextDouble()
    {
#if NET6_0_OR_GREATER
        return Random.Shared.NextDouble();
#else
        lock (Random)
            return Random.NextDouble();
#endif
    }
}
