namespace ActualLab.Fusion;

/// <summary>
/// Defines caching behavior for remote computed values.
/// </summary>
public enum RemoteComputedCacheMode
{
    Default = 0,
    Cache,
    NoCache,
    ReturnDefault,
}

/// <summary>
/// Extension methods for <see cref="RemoteComputedCacheMode"/>.
/// </summary>
internal static class RemoteComputedCacheModeExt
{
    // Cache and ReturnDefault are the modes whose computed-s carry an RpcCacheEntry:
    // they stay pseudo-registered once invalidated and can be served stale.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCacheOrReturnDefault(this RemoteComputedCacheMode mode)
        => mode is RemoteComputedCacheMode.Cache or RemoteComputedCacheMode.ReturnDefault;
}
