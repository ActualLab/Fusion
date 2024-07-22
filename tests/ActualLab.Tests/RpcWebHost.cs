using System.Reflection;
using Microsoft.Extensions.Hosting;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Server;
using ActualLab.Rpc.WebSockets;

#if NETFRAMEWORK
using Owin;
using System.Web.Http;
using ActualLab.Fusion.Server;
#else
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
#endif

namespace ActualLab.Tests;

public class RpcWebHost(
    IServiceCollection baseServices,
    Assembly? controllerAssembly = null
    ) : TestWebHostBase
{
    public IServiceCollection BaseServices { get; } = baseServices;
    public Assembly? ControllerAssembly { get; set; } = controllerAssembly;
    public Func<CpuTimestamp, int, Task>? WebSocketWriteDelayFactory { get; set; }
    public bool ExposeBackend { get; set; } = false;

    protected override void ConfigureHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services => {
            // Copy all services from the base service provider here
            services.AddRange(BaseServices);

            // Since we copy all services here,
            // only web-related ones must be added to services
            var webSocketServer = services.AddRpc().AddWebSocketServer();
            webSocketServer.Configure(_ => {
                var defaultOptions = RpcWebSocketServer.Options.Default;
                return defaultOptions with {
                    ExposeBackend = ExposeBackend,
                    WebSocketChannelOptions = defaultOptions.WebSocketChannelOptions with {
                        WriteDelayer = WebSocketWriteDelayFactory,
                    },
                };
            });
            if (ControllerAssembly != null) {
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
        builder.MapRpcServer(services);
    }
#endif
}
