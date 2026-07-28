using ActualLab.Caching;
using ActualLab.Plugins.Metadata;

namespace ActualLab.Plugins.Internal;

/// <summary>
/// Base class for <see cref="IPluginFinder"/> implementations that cache discovered plugin metadata.
/// </summary>
public abstract class CachingPluginFinderBase : IPluginFinder
{
    /// <summary>
    /// Configuration options for <see cref="CachingPluginFinderBase"/>.
    /// </summary>
    public record Options
    {
        public Func<IAsyncCache<string, string>>? CacheFactory { get; init; } = null;
    }

    private readonly Lazy<IAsyncCache<string, string>> _lazyCache;

    protected Options Settings { get; }
    protected ILogger Log { get; }
    protected IPluginInfoProvider PluginInfoProvider { get; }

    public IAsyncCache<string, string> Cache => _lazyCache.Value;
    public PluginSetInfo? FoundPlugins { get; private set; }

    protected CachingPluginFinderBase(
        Options settings,
        IPluginInfoProvider pluginInfoProvider,
        ILogger<CachingPluginFinderBase>? log = null)
    {
        Settings = settings;
        Log = log ?? NullLogger<CachingPluginFinderBase>.Instance;
        PluginInfoProvider = pluginInfoProvider;
        _lazyCache = new Lazy<IAsyncCache<string, string>>(settings.CacheFactory ?? CreateCache);
    }

    public async Task Run(CancellationToken cancellationToken = default)
        => FoundPlugins = await FindOrGetCachedPlugins(cancellationToken).ConfigureAwait(false);

    protected virtual async Task<PluginSetInfo> FindOrGetCachedPlugins(CancellationToken cancellationToken)
    {
        var cacheKey = GetCacheKey();
        if (cacheKey is null) {
            // Caching is off
            Log.LogDebug("Plugin cache is disabled (cache key is null)");
            return await FindPlugins(cancellationToken).ConfigureAwait(false);
        }

        var result = await Cache.TryGet(cacheKey, cancellationToken).ConfigureAwait(false);
        if (result.IsSome(out var v) && TryGetCachedPlugins(v) is { } cachedPluginSetInfo) {
            Log.LogDebug("Cached plugin set info found");
            return cachedPluginSetInfo;
        }

        Log.LogDebug("Cached plugin set info is not available; populating...");
        var pluginSetInfo = await FindPlugins(cancellationToken).ConfigureAwait(false);
        await Cache.Set(cacheKey, Serialize(pluginSetInfo), cancellationToken).ConfigureAwait(false);
        Log.LogDebug("Plugin set info is populated and cached");
        return pluginSetInfo;
    }

    protected virtual string Serialize(PluginSetInfo source)
        => SystemJsonSerialized.New(source).Data;

    protected virtual PluginSetInfo Deserialize(string source)
        => SystemJsonSerialized.New<PluginSetInfo?>(source).Value ?? PluginSetInfo.Empty;

    protected abstract IAsyncCache<string, string> CreateCache();
    protected abstract string? GetCacheKey();
    protected abstract Task<PluginSetInfo> FindPlugins(CancellationToken cancellationToken);

    // Every type named by the cache eventually reaches Type.GetType and ActivatorUtilities.CreateInstance,
    // so the cache must stay an optimization: implementations have to confirm the cached set names
    // nothing beyond what their own scan would have discovered.
    protected abstract bool IsValidCachedPluginSet(PluginSetInfo pluginSetInfo);

    // Private methods

    private PluginSetInfo? TryGetCachedPlugins(string data)
    {
        try {
            var pluginSetInfo = Deserialize(data);
            if (IsValidCachedPluginSet(pluginSetInfo))
                return pluginSetInfo;

            Log.LogWarning(
                "Cached plugin set info names plugins that weren't discovered by scanning, so it's rejected");
        }
        catch (Exception e) {
            Log.LogError(e, "Couldn't read cached plugin set info, so it's rejected");
        }
        return null;
    }
}
