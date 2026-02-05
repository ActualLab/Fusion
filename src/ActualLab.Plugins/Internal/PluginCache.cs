using ActualLab.OS;

namespace ActualLab.Plugins.Internal;

/// <summary>
/// Caches <see cref="IPluginInstanceHandle"/> instances by plugin implementation type.
/// </summary>
public interface IPluginCache
{
    public IPluginInstanceHandle GetOrCreate(Type pluginImplementationType);
}

/// <summary>
/// Default implementation of <see cref="IPluginCache"/> using a concurrent dictionary.
/// </summary>
public class PluginCache(IServiceProvider services) : IPluginCache
{
    private readonly IServiceProvider _services = services;
    private readonly ConcurrentDictionary<Type, IPluginInstanceHandle> _cache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public IPluginInstanceHandle GetOrCreate(Type pluginImplementationType)
        => _cache.GetOrAdd(pluginImplementationType, static (pluginImplementationType1, self) => {
            var handleType = typeof(IPluginInstanceHandle<>).MakeGenericType(pluginImplementationType1);
            return (IPluginInstanceHandle)self._services.GetRequiredService(handleType);
        }, this);
}
