using ActualLab.Plugins.Metadata;

namespace ActualLab.Plugins.Internal;

public interface IPluginFilter
{
    public bool IsEnabled(PluginInfo pluginInfo);
}

public class PredicatePluginFilter(Func<PluginInfo, bool> predicate) : IPluginFilter
{
    public bool IsEnabled(PluginInfo pluginInfo) => predicate(pluginInfo);
}
