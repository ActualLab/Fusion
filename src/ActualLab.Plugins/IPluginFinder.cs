using System.Diagnostics.CodeAnalysis;
using ActualLab.Plugins.Internal;
using ActualLab.Plugins.Metadata;

namespace ActualLab.Plugins;

public interface IPluginFinder
{
    PluginSetInfo? FoundPlugins { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Plugins)]
    Task Run(CancellationToken cancellationToken = default);
}
