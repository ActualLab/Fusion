using ActualLab.Rpc;
using ActualLab.Rpc.Server;
using Microsoft.AspNetCore.Builder;

// ReSharper disable once CheckNamespace
namespace Tutorial12;

public class Program
{
    public static void ConfigureServices(IServiceCollection services)
    {
        #region Part12_AddRpc
        var rpc = services.AddRpc(); // returns RpcBuilder
        #endregion

        #region Part12_AddServer
        rpc.AddServer<IMyService, MyService>(); // Expose IMyService resolved as MyService
        #endregion

        #region Part12_AddWebSocketServer
        rpc.AddWebSocketServer();
        #endregion
    }
}

public partial class Startup
{
    public void Configure(IApplicationBuilder app)
    {
        #region Part12_MapRpcWebSocketServer
        // And assuming you use minimal ASP.NET Core API:
        app.UseWebSockets(); // Adds WebSocket support to ASP.NET Core host
        app.UseEndpoints(endpoints => {
            endpoints.MapRpcWebSocketServer(); // Registers "/rpc/ws" endpoint
        });
        #endregion
    }
}

public partial class ClientProgram
{
    public static void ConfigureServices(IServiceCollection services, string serverUrl)
    {
        var rpc = services.AddRpc();

        #region Part12_AddClient
        rpc.AddClient<IMyService>(); // Adds a singleton IMyService, which is a client for this service
        // Some of alternatives:
        rpc.AddClient<IMyService>("myService"); // Consumes a IMyService named as "myService" on the server side
        #endregion

        #region Part12_AddWebSocketClient
        rpc.AddWebSocketClient(serverUrl);
        #endregion
    }

    public static async Task CallService(IServiceProvider serviceProvider)
    {
        #region Part12_CallService
        var myService = serviceProvider.GetRequiredService<IMyService>();
        Console.WriteLine(await myService.Ping());
        #endregion
    }
}

public interface IMyService : IRpcService
{
    public Task<string> Ping();
}

public class MyService : IMyService
{
    public Task<string> Ping() => Task.FromResult("Pong");
}
