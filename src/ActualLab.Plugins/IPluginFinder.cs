using System.Diagnostics.CodeAnalysis;
using ActualLab.Plugins.Internal;
using ActualLab.Plugins.Metadata;

namespace ActualLab.Plugins;

public interface IPluginFinder
{
    public PluginSetInfo? FoundPlugins { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Plugins)]
    public Task Run(CancellationToken cancellationToken = default);
}
