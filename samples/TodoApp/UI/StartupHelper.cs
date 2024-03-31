using Blazorise;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ActualLab.Fusion.Blazor;
using ActualLab.Fusion.Blazor.Authentication;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.Extensions;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.UI;
using ActualLab.OS;
using ActualLab.Rpc;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using Templates.TodoApp.Abstractions;
using Templates.TodoApp.Services;

namespace Templates.TodoApp.UI;

#pragma warning disable IL2026

public static class StartupHelper
{
    public static void ConfigureServices(IServiceCollection services, WebAssemblyHostBuilder builder)
    {
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Logging.AddFilter(typeof(App).Namespace, LogLevel.Information);
        builder.Logging.AddFilter(typeof(Computed).Namespace, LogLevel.Information);
        builder.Logging.AddFilter(typeof(RpcHub).Namespace, LogLevel.Debug);
        builder.Logging.AddFilter(typeof(CommandHandlerResolver).Namespace, LogLevel.Debug);

        // Fusion services
        var fusion = services.AddFusion();
        fusion.AddAuthClient();
        fusion.AddBlazor().AddAuthentication().AddPresenceReporter();

        var rpc = fusion.Rpc;
        rpc.AddWebSocketClient(builder.HostEnvironment.BaseAddress);
        // You may comment this out - the call below just enables RPC call logging
        services.AddSingleton<RpcPeerFactory>(_ =>
            static (hub, peerRef) => peerRef.IsServer
                ? throw new NotSupportedException("No server peers are allowed on the client.")
                : new RpcClientPeer(hub, peerRef) { CallLogLevel = LogLevel.Debug }
            );

        // Option 1: Client-side SimpleTodoService (no RPC)
        // fusion.AddService<ITodoService, SimpleTodoService>();

        // Option 2: Client-side TodoService and SandboxedKeyValueStore using InMemoryKeyValueStore (no RPC)
        // fusion.AddInMemoryKeyValueStore();
        // fusion.AddSandboxedKeyValueStore();
        // fusion.AddService<ITodoService, TodoService>();

        // Option 3: Client-side TodoService + remote SandboxedKeyValueStore -> DbKeyValueStore
        // fusion.AddClient<ISandboxedKeyValueStore>();
        // fusion.AddService<ITodos, TodoService>();

        // Option 4: Remote TodoService, SandboxedKeyValueStore, and DbKeyValueStore
        fusion.AddClient<ITodos>();

        ConfigureSharedServices(services);
    }

    public static void ConfigureSharedServices(IServiceCollection services)
    {
        IComputedState.DefaultOptions.FlowExecutionContext = true; // To preserve current culture

        // Blazorise
        services.AddBlazorise(options => options.Immediate = true)
            .AddBootstrap5Providers()
            .AddFontAwesomeIcons();

        // Other UI-related services
        var fusion = services.AddFusion();
        fusion.AddComputedGraphPruner(_ => new() { CheckPeriod = TimeSpan.FromSeconds(10) });
        fusion.AddFusionTime();
        fusion.AddService<TodoUI>(ServiceLifetime.Scoped);
        services.AddScoped(c => new RpcPeerStateMonitor(c, OSInfo.IsAnyClient ? RpcPeerRef.Default : null));

        // Default update delay is 0.5s
        services.AddScoped<IUpdateDelayer>(c => new UpdateDelayer(c.UIActionTracker(), 0.5));

        // Diagnostics
        services.AddHostedService(c => {
            var isWasm = OSInfo.IsWebAssembly;
            return new FusionMonitor(c) {
                SleepPeriod = isWasm
                    ? TimeSpan.Zero
                    : TimeSpan.FromMinutes(1).ToRandom(0.25),
                CollectPeriod = TimeSpan.FromSeconds(isWasm ? 3 : 60),
                AccessFilter = isWasm
                    ? static computed => computed.Input.Function is IClientComputeMethodFunction
                    : static computed => true,
                AccessStatisticsPreprocessor = StatisticsPreprocessor,
                RegistrationStatisticsPreprocessor = StatisticsPreprocessor,
            };

            void StatisticsPreprocessor(Dictionary<string, (int, int)> stats)
            {
                foreach (var key in stats.Keys.ToList()) {
                    if (key.Contains(".Pseudo"))
                        stats.Remove(key);
                    if (key.StartsWith("FusionTime."))
                        stats.Remove(key);
                }
            }
        });
    }
}
