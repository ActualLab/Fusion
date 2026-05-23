using System.Reflection;
using Microsoft.Extensions.Hosting;
using ActualLab.Rpc;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Server;
using ActualLab.Rpc.WebSockets;
using ActualLab.Testing.Logging;
using ActualLab.Testing.Web;

#if NETFRAMEWORK
using Owin;
using System.Web.Http;
using ActualLab.Fusion.Server;
#else
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
#endif

namespace ActualLab.Tests;

public class RpcWebHost(
    IServiceCollection baseServices,
    Assembly? controllerAssembly = null,
    bool useHttpClient = false,
    bool useHttps = false)
    : TestWebHostBase(useHttps)
{
    public IServiceCollection BaseServices { get; } = baseServices;
    public Assembly? ControllerAssembly { get; set; } = controllerAssembly;
    public Func<RpcFrameDelayer?>? RpcFrameDelayerFactory { get; set; }
    public bool UseHttpClient { get; } = useHttpClient;
    public bool UseHttps { get; } = useHttps;
    public bool ExposeBackend { get; set; } = false;

#if NETCOREAPP
    protected override HttpProtocols WebHostProtocols
        => UseHttpClient && !UseHttps
            ? HttpProtocols.Http2 // Cannot be Http1AndHttp2, coz protocol negotiation requires HTTPS
            : base.WebHostProtocols;
#endif

    protected override void ConfigureHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services => {
            // Copy all services from the base service provider here
            services.AddRange(BaseServices);

            // Since we copy all services here,
            // only web-related ones must be added to services
            var rpc = services.AddRpc();
            var webSocketServer = rpc.AddWebSocketServer();
            webSocketServer.Configure(_ => {
                var defaultOptions = RpcWebSocketServerOptions.Default;
                return defaultOptions with { ExposeBackend = ExposeBackend };
            });
#if NET5_0_OR_GREATER
            var httpServer = rpc.AddHttpServer();
            httpServer.Configure(_ => RpcHttpServerOptions.Default with { ExposeBackend = ExposeBackend });
#endif
            if (RpcFrameDelayerFactory is not null)
                services.AddSingleton<RpcWebSocketClientOptions>(_ => new RpcWebSocketClientOptions() {
                    FrameDelayerFactory = RpcFrameDelayerFactory,
                });
            if (ControllerAssembly is not null) {
#if NETFRAMEWORK
                var controllerTypes = ControllerAssembly.GetControllerTypes().ToArray();
                services.AddControllersAsServices(controllerTypes);
#else
                services.AddControllers().AddApplicationPart(ControllerAssembly);
                services.AddHostedService<ApplicationPartsLogger>();
#endif
            }
        });
    }

#if NETCOREAPP
    protected override void ConfigureWebHost(IWebHostBuilder webHost)
    {
        webHost.Configure((_, app) => {
            app.UseWebSockets();
            app.UseRouting();
            app.UseEndpoints(endpoints => {
                endpoints.MapControllerRoute(name: "DefaultApi", pattern: "api/{controller}/{action}");
                endpoints.MapControllers();
                endpoints.MapRpcWebSocketServer();
#if NET5_0_OR_GREATER
                endpoints.MapRpcHttpServer();
#endif
            });
        });
    }
#else
    protected override void ConfigureHttp(IServiceProvider services, HttpConfiguration config)
    {
        base.ConfigureHttp(services, config);
        config.Formatters.Insert(0, new TextMediaTypeFormatter());
    }

    protected override void ConfigureAppBuilder(IServiceProvider services, IAppBuilder builder)
    {
        base.ConfigureAppBuilder(services, builder);
        builder.MapRpcWebSocketServer(services);
    }
#endif
}
