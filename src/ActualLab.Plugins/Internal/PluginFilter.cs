using ActualLab.Plugins.Metadata;

namespace ActualLab.Plugins.Internal;

/// <summary>
/// Defines a filter that determines whether a plugin should be enabled.
/// </summary>
public interface IPluginFilter
{
    public bool IsEnabled(PluginInfo pluginInfo);
}

/// <summary>
/// An <see cref="IPluginFilter"/> implementation that delegates to a predicate function.
/// </summary>
public class PredicatePluginFilter(Func<PluginInfo, bool> predicate) : IPluginFilter
{
    public bool IsEnabled(PluginInfo pluginInfo) => predicate(pluginInfo);
}
