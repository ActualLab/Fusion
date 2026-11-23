namespace ActualLab.Plugins.Internal;

/// <summary>
/// Creates plugin instances from their implementation types using dependency injection.
/// </summary>
public interface IPluginFactory
{
    public object? Create(Type pluginType);
}

/// <summary>
/// Default <see cref="IPluginFactory"/> that creates plugin instances via the service provider.
/// </summary>
public class PluginFactory(IServiceProvider services) : IPluginFactory
{
    protected IServiceProvider Services { get; } = services;

    [UnconditionalSuppressMessage("Trimming", "IL2092", Justification = "We assume plugin types are fully preserved")]
    public virtual object? Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type pluginType)
        => Services.CreateInstance(pluginType);
}
