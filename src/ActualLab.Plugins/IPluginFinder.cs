using ActualLab.Plugins.Metadata;

namespace ActualLab.Plugins;

/// <summary>
/// Discovers available plugins and populates <see cref="PluginSetInfo"/>.
/// </summary>
public interface IPluginFinder
{
    public PluginSetInfo? FoundPlugins { get; }

    public Task Run(CancellationToken cancellationToken = default);
}
