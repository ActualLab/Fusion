using ActualLab.Plugins.Metadata;

namespace ActualLab.Plugins;

public interface IPluginFinder
{
    public PluginSetInfo? FoundPlugins { get; }

    public Task Run(CancellationToken cancellationToken = default);
}
