using ActualLab.Plugins.Internal;
using Errors = ActualLab.Plugins.Internal.Errors;

namespace ActualLab.Plugins;

public class PluginHostBuilder
{
    public IServiceCollection Services { get; set; }
    public Func<IServiceCollection, IServiceProvider> ServiceProviderFactory { get; set; } =
        services => new DefaultServiceProviderFactory().CreateServiceProvider(services);

    public PluginHostBuilder(IServiceCollection? services = null)
    {
        services ??= new ServiceCollection();
        Services = services;
        if (!services.HasService<ILoggerFactory>())
            services.AddLogging();

        // Own services
        services.AddSingleton<IPluginHost>(c => new PluginHost(c));
        services.AddSingleton<IPluginFactory>(c => new PluginFactory(c));
        services.AddSingleton<IPluginCache>(c => new PluginCache(c));
        services.AddSingleton<IPluginInfoProvider>(_ => new PluginInfoProvider());
        services.AddSingleton(typeof(IPluginInstanceHandle<>), typeof(PluginInstanceHandle<>));
        services.AddSingleton(typeof(IPluginHandle<>), typeof(PluginHandle<>));
        services.AddSingleton(c => {
            var pluginFinder = c.GetRequiredService<IPluginFinder>();
            return pluginFinder.FoundPlugins
                ?? throw Errors.PluginFinderRunFailed(pluginFinder.GetType());
        });

        // FileSystemPluginFinder is the default IPluginFinder
        services.AddSingleton(_ => new FileSystemPluginFinder.Options());
        services.AddSingleton<IPluginFinder>(c => new FileSystemPluginFinder(
            c.GetRequiredService<FileSystemPluginFinder.Options>(),
            c.GetRequiredService<IPluginInfoProvider>(),
            c.LogFor<FileSystemPluginFinder>()));
    }

    public IPluginHost Build()
        => Task.Run(() => BuildAsync()).GetAwaiter().GetResult();

    public virtual async Task<IPluginHost> BuildAsync(CancellationToken cancellationToken = default)
    {
        var services = ServiceProviderFactory.Invoke(Services);
        var pluginFinder = services.GetRequiredService<IPluginFinder>();
        await pluginFinder.Run(cancellationToken).ConfigureAwait(false);
        var host = services.GetRequiredService<IPluginHost>();
        return host;
    }
}
