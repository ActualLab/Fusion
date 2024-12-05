using ActualLab.Plugins.Metadata;

namespace ActualLab.Plugins.Internal;

public class PredefinedPluginFinder : IPluginFinder
{
    public record Options
    {
        public IEnumerable<Type> PluginTypes { get; init; } = Enumerable.Empty<Type>();
        public bool ResolveIndirectDependencies { get; init; }
    }

    public PluginSetInfo FoundPlugins { get; }

    // ReSharper disable once ConvertToPrimaryConstructor
    public PredefinedPluginFinder(
        Options settings,
        IPluginInfoProvider pluginInfoProvider)
        // ReSharper disable once ConvertToPrimaryConstructor
    {
        FoundPlugins = new(
            settings.PluginTypes.Distinct(),
            pluginInfoProvider,
            settings.ResolveIndirectDependencies);
    }

    public Task Run(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
