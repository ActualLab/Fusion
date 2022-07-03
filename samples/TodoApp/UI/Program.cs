using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Stl.DependencyInjection;

namespace Templates.TodoApp.UI;

public class Program
{
    public static Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        StartupHelper.ConfigureServices(builder.Services, builder);
        var host = builder.Build();
        _ = host.Services.HostedServices().Start();
        return host.RunAsync();
    }
}
