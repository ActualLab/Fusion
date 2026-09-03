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
public static class RemoteComputedCacheModeExt
{
    // Cache and ReturnDefault are the modes whose computeds carry an RpcCacheEntry:
    // they stay pseudo-registered once invalidated and can be served stale.
    public static bool UsesCacheEntry(this RemoteComputedCacheMode mode)
        => mode is RemoteComputedCacheMode.Cache or RemoteComputedCacheMode.ReturnDefault;
}
