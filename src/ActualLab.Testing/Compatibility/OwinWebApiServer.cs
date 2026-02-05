#if NETFRAMEWORK

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Dependencies;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Owin.Hosting;
using Owin;

// ReSharper disable once CheckNamespace
namespace ActualLab.Testing;

#pragma warning disable CA1812 // C is an internal class that is apparently never instantiated

/// <summary>
/// Configuration options for the OWIN-based Web API test server.
/// </summary>
public class OwinWebApiServerOptions
{
    public string Urls { get; set; } = null!;
    public Action<IServiceProvider,IAppBuilder> ConfigureBuilder { get; set; } = null!;
    public Action<IServiceProvider,HttpConfiguration> ConfigureHttp { get; set; } = null!;
}

/// <summary>
/// An OWIN-based <see cref="IServer"/> implementation for hosting
/// Web API endpoints in .NET Framework test environments.
/// </summary>
internal sealed class OwinWebApiServer : IServer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ServerAddressesFeature _serverAddresses;
    private bool _hasStarted;
#pragma warning disable 169
#pragma warning disable CA1823 // Unused field
    private int _stopping;
#pragma warning restore CA1823
#pragma warning restore 169

    private readonly OwinWebApiServerOptions options;
    //private readonly CancellationTokenSource _stopCts = new CancellationTokenSource();
    //private readonly TaskCompletionSource _stoppedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    private IDisposable _application = null!;

    public IFeatureCollection Features { get; }

    public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
    {
        if (_hasStarted)
            throw new InvalidOperationException("The server has already started and/or has not been cleaned up yet");
        _hasStarted = true;

        string baseAddress = options.Urls;
        Action<IAppBuilder> configureBuilder = (appBuilder) => options.ConfigureBuilder(_serviceProvider, appBuilder);
        Action<HttpConfiguration> setupConfiguration = (config) => options.ConfigureHttp(_serviceProvider, config);
        _application = WebApp.Start(baseAddress, new WebApiStartup(_serviceProvider, setupConfiguration, configureBuilder).Configuration);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _application?.Dispose();
        _application = null!;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopAsync(new CancellationToken(canceled: true)).GetAwaiter().GetResult();
    }

    public OwinWebApiServer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        options = serviceProvider.GetRequiredService<IOptions<OwinWebApiServerOptions>>().Value;
        Features = new FeatureCollection();
        _serverAddresses = new ServerAddressesFeature();
        Features.Set<IServerAddressesFeature>(_serverAddresses);
        _serverAddresses.Addresses.Add(options.Urls);
    }
}

/// <summary>
/// OWIN startup configuration class that sets up Web API routing
/// and dependency injection for the test server.
/// </summary>
internal sealed class WebApiStartup
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Action<HttpConfiguration> _setupConfiguration;
    private readonly Action<IAppBuilder> _configureAppBuilder;

    public WebApiStartup(IServiceProvider serviceProvider, Action<HttpConfiguration> setupConfiguration,
        Action<IAppBuilder> configureAppBuilder)
    {
        _serviceProvider = serviceProvider;
        _setupConfiguration = setupConfiguration;
        _configureAppBuilder = configureAppBuilder;
    }

    public void Configuration(IAppBuilder appBuilder)
    {
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=316888

        // Configure Web API for self-host.
        var config = new HttpConfiguration();
        Configure(config);

        if (_configureAppBuilder is not null)
            _configureAppBuilder(appBuilder);
        var appBuilders = _serviceProvider.GetServices<Action<IAppBuilder>>();
        foreach (var service in appBuilders) {
            service(appBuilder);
        }

        appBuilder.UseWebApi(config);
    }

    private void Configure(HttpConfiguration config)
    {
        if (_setupConfiguration is not null)
            _setupConfiguration(config);
        var configBuilders = _serviceProvider.GetServices<Action<HttpConfiguration>>();
        foreach (var service in configBuilders) {
            service(config);
        }
        config.DependencyResolver = new DefaultDependencyResolver(_serviceProvider);

        config.MapHttpAttributeRoutes();

        config.Routes.MapHttpRoute(
            name: "DefaultApi",
            routeTemplate: "api/{controller}/{action}"
        );
    }
}

/// <summary>
/// An <see cref="IDependencyResolver"/> that wraps an <see cref="IServiceProvider"/>
/// for Web API dependency injection in OWIN test hosts.
/// </summary>
public class DefaultDependencyResolver : IDependencyResolver
{
    private IServiceProvider serviceProvider;

    public DefaultDependencyResolver(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public object GetService(Type serviceType)
    {
        var service = this.serviceProvider.GetService(serviceType);
        return service;
    }

    public IEnumerable<object> GetServices(Type serviceType)
    {
        var services = this.serviceProvider.GetServices(serviceType);
        return services!;
    }

    public void Dispose()
    {
    }

    public IDependencyScope BeginScope()
    {
        return this;
    }
}

/// <summary>
/// An <see cref="IHostedService"/> that starts and stops an <see cref="IServer"/>
/// as part of the generic host lifecycle in test environments.
/// </summary>
internal sealed class GenericWebHostService : IHostedService
{
    private readonly IServer _server;

    public GenericWebHostService(IServer server) => this._server = server;

    public Task StartAsync(CancellationToken cancellationToken)
        => _server.StartAsync(new HostingApplication(), cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken)
        => _server.StopAsync(cancellationToken);
}

#endif
