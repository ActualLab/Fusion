using Blazorise;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ActualLab.Fusion.Blazor;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.Extensions;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Fusion.UI;
using ActualLab.OS;
using ActualLab.Rpc;
using ActualLab.Rpc.Clients;
using Blazored.LocalStorage;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        logging.AddFilter(typeof(App).Namespace, LogLevel.Debug);
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
        RpcSerializationFormatResolver.Default = new("json5np"); // mempack6c, mempack6, json5, etc.

        // The block of code below is totally optional.
        // It makes Fusion to delay initial compute method RCP calls if they're resolved as "hit" into the local cache.
        // I.e., we can postpone a majority (or all) of RPC calls on startup to let the app start a bit faster.
        // Note that it makes sense only if there are hundreds or thousands of such calls.
        // We use a single instance of the initial delay task - we want it to be
        // an absolute delay from the app start rather than a relative delay for each call.
        var hitToCallDelayTask = Task
            .Delay(TimeSpan.FromSeconds(1)) // Initial delay of 1 second
            .ContinueWith(_ => RemoteComputedCache.HitToCallDelayer = null, TaskScheduler.Default); // Reset the delayer once the initial delay is over
        RemoteComputedCache.HitToCallDelayer = (input, peer) => {
            peer.InternalServices.Log.LogDebug("'{PeerRef}': Delaying {Input}", peer.Ref, input);
            return hitToCallDelayTask;
        };
        // ComputedSynchronizer.DefaultCurrent = ComputedSynchronizer.Precise.Instance;

        // Fusion services
        var fusion = services.AddFusion();
        fusion.AddClient<IUserApi>(); // Client-facing auth service

        // Local Blazor auth (replaces fusion.AddBlazor().AddAuthentication().AddPresenceReporter())
        fusion.AddBlazor();
        services.AddAuthorizationCore();
        services.RemoveAll(typeof(AuthenticationStateProvider));
        services.AddSingleton(_ => AuthStateProvider.Options.Default);
        services.AddScoped<AuthenticationStateProvider>(c => new AuthStateProvider(
            c.GetRequiredService<AuthStateProvider.Options>(), c));
        services.AddScoped(c => (AuthStateProvider)c.GetRequiredService<AuthenticationStateProvider>());
        services.AddScoped(c => new ClientAuthHelper(c));
        services.AddSingleton(_ => PresenceReporter.Options.Default);
        services.AddScoped(c => new PresenceReporter(c.GetRequiredService<PresenceReporter.Options>(), c));

        // RPC clients
        fusion.AddClient<ITodoApi>();
        fusion.AddClient<IStockApi>();
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
            RpcWebSocketClientOptions.Default = new RpcWebSocketClientOptions() {
                UseAutoFrameDelayerFactory = true,
            };
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

        // The default invalidation tracking mode is OriginOnly, which is typically enough,
        // WholeChain use may lead to excessive memory usage. See its description.
#if false
        Invalidation.TrackingMode = InvalidationTrackingMode.WholeChain;
#endif
        services.AddHostedService(c => {
            var isWasm = OSInfo.IsWebAssembly;
            return new FusionMonitor(c) {
                SleepPeriod = TimeSpan.FromSeconds(isWasm ? 0 : 15),
                CollectPeriod = TimeSpan.FromSeconds(isWasm ? 5 : 15),
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
