using System.Diagnostics.CodeAnalysis;
using Blazorise;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ActualLab.Fusion.Blazor;
using ActualLab.Fusion.Blazor.Authentication;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.Extensions;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Fusion.UI;
using ActualLab.OS;
using ActualLab.Rpc;
using Blazored.LocalStorage;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using Samples.TodoApp.Abstractions;
using Samples.TodoApp.UI.Services;

namespace Samples.TodoApp.UI;

[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCodeAttribute", Justification = "Fine here")]
public static class ClientStartup
{
    public static void ConfigureServices(IServiceCollection services, WebAssemblyHostBuilder builder)
    {
        var logging = builder.Logging;
        logging.SetMinimumLevel(LogLevel.Warning);
        logging.AddFilter(typeof(App).Namespace, LogLevel.Information);
        logging.AddFilter(typeof(Computed).Namespace, LogLevel.Information);
        logging.AddFilter(typeof(InMemoryRemoteComputedCache).Namespace, LogLevel.Information);
        logging.AddFilter(typeof(RpcHub).Namespace, LogLevel.Debug);
        logging.AddFilter(typeof(CommandHandlerResolver).Namespace, LogLevel.Debug);
        logging.AddFilter(typeof(IRemoteComputedCache).Namespace, LogLevel.Debug);
        logging.AddFilter(typeof(ComponentInfo).Namespace, LogLevel.Debug);
#if DEBUG // Log cache entry updates in debug mode to see if our serialization results are identical for the same output
        RemoteComputeServiceInterceptor.Options.Default = new() {
            LogCacheEntryUpdateSettings = (LogLevel.Warning, int.MaxValue),
        };
#endif
        // Default RPC client serialization format
        RpcSerializationFormatResolver.Default = RpcSerializationFormatResolver.Default with {
            // DefaultClientFormatKey = "mempack3c",
            // DefaultClientFormatKey = "msgpack3c",
            // DefaultClientFormatKey = "json3",
        };

        // Fusion services
        var fusion = services.AddFusion();
        fusion.AddAuthClient();
        fusion.AddBlazor().AddAuthentication().AddPresenceReporter();

        // RPC clients
        fusion.AddClient<ITodoApi>();
        fusion.Rpc.AddClient<ISimpleService>();

        // Client-side RPC services (client-side servers callable from the server side)
        fusion.Rpc.AddServer<ISimpleClientSideService, SimpleClientSideService>();

        // LocalStorageRemoteComputedCache as IRemoteComputedCache
        services.AddBlazoredLocalStorageAsSingleton();
        services.AddSingleton(_ => LocalStorageRemoteComputedCache.Options.Default);
        services.AddSingleton(c => {
            var options = c.GetRequiredService<LocalStorageRemoteComputedCache.Options>();
            return (IRemoteComputedCache)new LocalStorageRemoteComputedCache(options, c);
        });

        ConfigureSharedServices(services, HostKind.Client, builder.HostEnvironment.BaseAddress);
    }

    public static void ConfigureSharedServices(IServiceCollection services, HostKind hostKind, string remoteRpcHostUrl)
    {
#if false
        // Uncomment to see how ComputedGraphPruner works
        ComputedGraphPruner.Options.Default = new() {
            CheckPeriod = TimeSpan.FromSeconds(10), // Default is 5 min.
        };
#endif
        var fusion = services.AddFusion();
        fusion.AddFusionTime(); // Add it only if you use it

        if (hostKind != HostKind.BackendServer) {
            // Client and API host settings

            // Highly recommended option for client & API servers:
            RpcDefaultDelegates.FrameDelayerProvider = RpcFrameDelayerProviders.Auto();
            // Lets ComputedState to be dependent on, e.g., current culture - use only if you need this:
            // ComputedState.DefaultOptions.FlowExecutionContext = true;
            fusion.Rpc.AddWebSocketClient(remoteRpcHostUrl);
            if (hostKind is HostKind.ApiServer or HostKind.SingleServer) {
                // All server-originating RPC connections should go to the default backend server
                RpcPeerRef.Default = RpcPeerRef.GetDefaultPeerRef(isBackend: true);
                // And want to call the client via this server-side RPC client:
                fusion.Rpc.AddClient<ISimpleClientSideService>();
            }

            // If we're here, hostKind is Client, ApiServer, or SingleServer
            fusion.AddComputeService<Todos>(ServiceLifetime.Scoped);
            services.AddScoped(c => new RpcPeerStateMonitor(c, OSInfo.IsAnyClient ? RpcPeerRef.Default : null));
            services.AddScoped<IUpdateDelayer>(c => new UpdateDelayer(c.UIActionTracker(), 0.25)); // 0.25s

            // Uncomment to make computed state components to re-render only on re-computation of their state.
            // Click on DefaultOptions to see when they re-render by default.
            // ComputedStateComponent.DefaultOptions = ComputedStateComponentOptions.RecomputeStateOnParameterChange;
            // ComputedRenderStateComponent.DefaultOptions = ComputedStateComponentOptions.RecomputeStateOnParameterChange;

            // Blazorise
            services.AddBlazorise(options => {
                    options.Immediate = true;
                    options.Debounce = true;
                })
                .AddBootstrap5Providers()
                .AddFontAwesomeIcons();
        }

        // Diagnostics
        if (hostKind == HostKind.Client)
            RpcPeer.DefaultCallLogLevel = LogLevel.Debug;
        services.AddHostedService(c => {
            var isWasm = OSInfo.IsWebAssembly;
            return new FusionMonitor(c) {
                SleepPeriod = isWasm
                    ? TimeSpan.Zero
                    : TimeSpan.FromMinutes(1).ToRandom(0.25),
                CollectPeriod = TimeSpan.FromSeconds(isWasm ? 3 : 60),
                AccessFilter = isWasm
                    ? static computed => computed.Input.Function is RemoteComputeMethodFunction
                    : static _ => true,
                AccessStatisticsPreprocessor = StatisticsPreprocessor,
                RegistrationStatisticsPreprocessor = StatisticsPreprocessor,
            };

            void StatisticsPreprocessor(Dictionary<string, (int, int)> stats)
            {
                foreach (var key in stats.Keys.ToList()) {
                    if (key.Contains(".Pseudo"))
                        stats.Remove(key);
                    if (key.StartsWith("FusionTime.", StringComparison.Ordinal))
                        stats.Remove(key);
                }
            }
        });
    }
}
