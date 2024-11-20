using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Plugins.Internal;

public interface IPluginFactory
{
    public object? Create(Type pluginType);
}

public class PluginFactory(IServiceProvider services) : IPluginFactory
{
    protected IServiceProvider Services { get; } = services;

#pragma warning disable IL2092
    public virtual object? Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type pluginType)
        => Services.CreateInstance(pluginType);
#pragma warning restore IL2092
}
