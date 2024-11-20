using ActualLab.Plugins.Metadata;

namespace ActualLab.Plugins;

public interface IPluginHost : IServiceProvider, IAsyncDisposable, IDisposable
{
    public PluginSetInfo FoundPlugins { get; }
    // Return actual IServiceProvider hosting plugins
    public IServiceProvider Services { get; }
}

public class PluginHost(IServiceProvider services) : IPluginHost
{
    public IServiceProvider Services { get; } = services;
    public PluginSetInfo FoundPlugins { get; } = services.GetRequiredService<PluginSetInfo>();

    public virtual ValueTask DisposeAsync()
        => Services is IAsyncDisposable ad
            ? ad.DisposeAsync()
            : default;

    public virtual void Dispose()
    {
        if (Services is IDisposable d)
            d.Dispose();
    }

    public object? GetService(Type serviceType)
        => Services.GetService(serviceType);
}
