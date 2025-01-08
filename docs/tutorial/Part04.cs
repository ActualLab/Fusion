using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ActualLab.Fusion;
using ActualLab.Fusion.Server;
using ActualLab.Rpc.Server;
using static System.Console;

namespace Tutorial.Part04_Classes
{
    #region Part04_CommonServices
    // Ideally, we want Compute Service client to be exactly the same as corresponding
    // Compute Service. A good way to enforce this is to expose an interface
    // that should be implemented by Compute Service + tell Fusion to "expose"
    // the client via the same interface.
    public interface ICounterService : IComputeService
    {
        [ComputeMethod]
        Task<int> Get(string key, CancellationToken cancellationToken = default);
        Task Increment(string key, CancellationToken cancellationToken = default);
        Task SetOffset(int offset, CancellationToken cancellationToken = default);
    }
    #endregion

    #region Part04_HostServices
    public class CounterService : ICounterService
    {
        private readonly ConcurrentDictionary<string, int> _counters = new ConcurrentDictionary<string, int>();
        private readonly IMutableState<int> _offset;

        public CounterService(StateFactory stateFactory)
            => _offset = stateFactory.NewMutable<int>();

        [ComputeMethod] // Optional: this attribute is inherited from interface
        public virtual async Task<int> Get(string key, CancellationToken cancellationToken = default)
        {
            WriteLine($"{nameof(Get)}({key})");
            var offset = await _offset.Use(cancellationToken);
            return offset + (_counters.TryGetValue(key, out var value) ? value : 0);
        }

        public Task Increment(string key, CancellationToken cancellationToken = default)
        {
            WriteLine($"{nameof(Increment)}({key})");
            _counters.AddOrUpdate(key, k => 1, (k, v) => v + 1);
            using (Invalidation.Begin())
                _ = Get(key, default);
            return Task.CompletedTask;
        }

        public Task SetOffset(int offset, CancellationToken cancellationToken = default)
        {
            WriteLine($"{nameof(SetOffset)}({offset})");
            _offset.Value = offset;
            return Task.CompletedTask;
        }
    }
    #endregion
}

namespace Tutorial
{
    using Part04_Classes;

    public static class Part04
    {
        #region Part04_CreateXxx
        public static IHost CreateHost()
        {
            var builder = Host.CreateDefaultBuilder();
            builder.ConfigureHostConfiguration(cfg =>
                cfg.AddInMemoryCollection(new Dictionary<string, string>() {{"Environment", "Development"}}));
            builder.ConfigureLogging(logging =>
                logging.ClearProviders().SetMinimumLevel(LogLevel.Information).AddDebug());
            builder.ConfigureServices((b, services) => {
                var fusion = services.AddFusion();
                fusion.AddWebServer();
                // Registering Compute Service
                fusion.AddService<ICounterService, CounterService>();
            });
            builder.ConfigureWebHost(webHost => {
                webHost.UseKestrel();
                webHost.UseUrls("http://localhost:50050/");
                webHost.Configure((ctx, app) => {
                    app.UseWebSockets();
                    app.UseRouting();
                    app.UseEndpoints(endpoints => {
                        endpoints.MapRpcWebSocketServer();
                    });
                });
            });
            return builder.Build();
        }

        public static IServiceProvider CreateClientServices()
        {
            var services = new ServiceCollection();
            var baseUri = new Uri($"http://localhost:50050/");

            var fusion = services.AddFusion();
            fusion.Rpc.AddWebSocketClient(baseUri);
            fusion.AddClient<ICounterService>();

            return services.BuildServiceProvider();
        }
        #endregion

        public static async Task ReplicaService()
        {
            #region Part04_ReplicaService
            using var host = CreateHost();
            await host.StartAsync();
            WriteLine("Host started.");

            using var stopCts = new CancellationTokenSource();
            var cancellationToken = stopCts.Token;

            async Task Watch<T>(string name, Computed<T> computed)
            {
                for (;;) {
                    WriteLine($"{name}: {computed.Value}, {computed}");
                    await computed.WhenInvalidated(cancellationToken);
                    WriteLine($"{name}: {computed.Value}, {computed}");
                    computed = await computed.Update(cancellationToken);
                }
            }

            var services = CreateClientServices();
            var counters = services.GetRequiredService<ICounterService>();
            var aComputed = await Computed.Capture(() => counters.Get("a"));
            _ = Task.Run(() => Watch(nameof(aComputed), aComputed));
            var bComputed = await Computed.Capture(() => counters.Get("b"));
            _ = Task.Run(() => Watch(nameof(bComputed), bComputed));

            await Task.Delay(200);
            await counters.Increment("a");
            await Task.Delay(200);
            await counters.SetOffset(10);
            await Task.Delay(200);

            stopCts.Cancel();
            await host.StopAsync();
            #endregion
        }

        public static async Task LiveStateFromReplica()
        {
            #region Part04_LiveStateFromReplica
            using var host = CreateHost();
            await host.StartAsync();
            WriteLine("Host started.");

            var services = CreateClientServices();
            var counters = services.GetRequiredService<ICounterService>();
            var stateFactory = services.StateFactory();
            using var state = stateFactory.NewComputed(
                new ComputedState<string>.Options() {
                    UpdateDelayer = FixedDelayer.Get(1), // 1 second update delay
                    EventConfigurator = state1 => {
                        // A shortcut to attach 3 event handlers: Invalidated, Updating, Updated
                        state1.AddEventHandler(StateEventKind.All,
                            (s, e) => WriteLine($"{DateTime.Now}: {e}, Value: {s.Value}, Computed: {s.Computed}"));
                    },
                },
                async (state, cancellationToken) => {
                    var counter = await counters.Get("a", cancellationToken);
                    return $"counters.Get(a) -> {counter}";
                });
            await state.Update(); // Ensures the state gets up-to-date value
            await counters.Increment("a");
            await Task.Delay(2000);
            await counters.SetOffset(10);
            await Task.Delay(2000);

            await host.StopAsync();
            #endregion
        }
    }
}
