using Microsoft.EntityFrameworkCore;
using Samples.HelloCart.V2;
using ActualLab.Fusion.Server;
using ActualLab.Rpc;
using ActualLab.Rpc.Server;
using Samples.HelloCart.V3;

namespace Samples.HelloCart.V4;

public class AppV4 : AppBase
{
    public WebApplication App { get; protected set; }

    public AppV4()
    {
        var uri = "http://localhost:7005";

        // Create server
        App = CreateWebApp(uri);
        ServerServices = App.Services;

        // Create client
        ClientServices = BuildClientServices(uri);
    }

    protected WebApplication CreateWebApp(string baseUri)
    {
        var builder = WebApplication.CreateBuilder();

        // Configure services
        var services = builder.Services;
        AppLogging.Configure(services);
        AppDb.Configure(services);
        services.AddFusion(RpcServiceMode.Server, fusion => {
            fusion.AddWebServer();
            fusion.AddService<IProductService, DbProductServiceUsingEntityResolver>();
            fusion.AddService<ICartService, DbCartServiceUsingEntityResolver>();
        });

        // Configure WebApplication
        var app = builder.Build();
        app.Urls.Add(baseUri);
        app.UseFusionSession();
        app.UseWebSockets(new WebSocketOptions() {
            KeepAliveInterval = TimeSpan.FromSeconds(30),
        });
        app.MapRpcWebSocketServer();
        return app;
    }

    protected IServiceProvider BuildClientServices(string baseUri)
    {
        var services = new ServiceCollection();
        AppLogging.Configure(services);
        services.AddFusion(fusion => {
            fusion.Rpc.AddWebSocketClient(baseUri);
            fusion.AddClient<IProductService>();
            fusion.AddClient<ICartService>();
        });
        return services.BuildServiceProvider();
    }

    public override async Task InitializeAsync(IServiceProvider services, bool startHostedServices)
    {
        // Let's re-create the database first
        await using var dbContext = ServerServices.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        await base.InitializeAsync(services, false);
        if (startHostedServices) {
            await App.StartAsync();
            await Task.Delay(100);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        // Let's stop the client first
        if (ClientServices is IAsyncDisposable csd)
            await csd.DisposeAsync();

        await App.StopAsync();
        await App.DisposeAsync();
    }
}
