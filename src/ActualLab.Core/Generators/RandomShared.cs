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
