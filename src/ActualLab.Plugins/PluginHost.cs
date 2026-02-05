using ActualLab.Plugins.Metadata;

namespace ActualLab.Plugins;

/// <summary>
/// Represents the plugin host that provides access to discovered plugins and their services.
/// </summary>
public interface IPluginHost : IServiceProvider, IAsyncDisposable, IDisposable
{
    public PluginSetInfo FoundPlugins { get; }
    // Return actual IServiceProvider hosting plugins
    public IServiceProvider Services { get; }
}

/// <summary>
/// Default implementation of <see cref="IPluginHost"/> that wraps an
/// <see cref="IServiceProvider"/> and exposes discovered plugin metadata.
/// </summary>
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
