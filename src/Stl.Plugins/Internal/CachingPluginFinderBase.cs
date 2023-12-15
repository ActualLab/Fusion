using System.Diagnostics.CodeAnalysis;
using ActualLab.Caching;
using ActualLab.Plugins.Metadata;

namespace ActualLab.Plugins.Internal;

public abstract class CachingPluginFinderBase : IPluginFinder
{
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

    [RequiresUnreferencedCode(UnreferencedCode.Plugins)]
    public async Task Run(CancellationToken cancellationToken = default)
        => FoundPlugins = await FindOrGetCachedPlugins(cancellationToken).ConfigureAwait(false);

    [RequiresUnreferencedCode(UnreferencedCode.Plugins)]
    protected virtual async Task<PluginSetInfo> FindOrGetCachedPlugins(CancellationToken cancellationToken)
    {
        var cacheKey = GetCacheKey();
        if (cacheKey == null) {
            // Caching is off
            Log.LogDebug("Plugin cache is disabled (cache key is null)");
            return await FindPlugins(cancellationToken).ConfigureAwait(false);
        }
        PluginSetInfo pluginSetInfo;
        var result = await Cache.TryGet(cacheKey, cancellationToken).ConfigureAwait(false);
        if (result.IsSome(out var v)) {
            Log.LogDebug("Cached plugin set info found");
            try {
                pluginSetInfo = Deserialize(v);
                return pluginSetInfo;
            }
            catch (Exception e) {
                Log.LogError(e, "Couldn't deserialize cached plugin set info");
            }
        }
        Log.LogDebug("Cached plugin set info is not available; populating...");
        pluginSetInfo = await FindPlugins(cancellationToken).ConfigureAwait(false);
        await Cache.Set(cacheKey, Serialize(pluginSetInfo), cancellationToken).ConfigureAwait(false);
        Log.LogDebug("Plugin set info is populated and cached");
        return pluginSetInfo;
    }

    [RequiresUnreferencedCode(Stl.Internal.UnreferencedCode.Serialization)]
    protected virtual string Serialize(PluginSetInfo source)
        => NewtonsoftJsonSerialized.New(source).Data;

    [RequiresUnreferencedCode(Stl.Internal.UnreferencedCode.Serialization)]
    protected virtual PluginSetInfo Deserialize(string source)
        => new NewtonsoftJsonSerialized<PluginSetInfo?>(source).Value ?? PluginSetInfo.Empty;

    protected abstract IAsyncCache<string, string> CreateCache();
    protected abstract string? GetCacheKey();
    [RequiresUnreferencedCode(UnreferencedCode.Plugins)]
    protected abstract Task<PluginSetInfo> FindPlugins(CancellationToken cancellationToken);
}
