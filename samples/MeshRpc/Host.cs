using ActualLab.Async;
using ActualLab.Fusion;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Server;
using ActualLab.Rpc;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Server;
using ActualLab.Text;
using Pastel;
using Samples.MeshRpc.Services;
using static Samples.MeshRpc.HostFactorySettings;

namespace Samples.MeshRpc;

public sealed class Host : WorkerBase
{
    private static int _lastId;

    public Symbol Id { get; }
    public HostRef Ref { get; }
    public int Hash { get; }
    public int PortSlot { get; }
    public RpcServiceMode ServiceMode { get; }
    public bool UseRemoteComputedCache { get; }
    public string Url { get; }
    public WebApplication App { get; }
    public IServiceProvider Services => App.Services;

    public static string GetUrl(int portSlot)
        => $"http://localhost:{22222 + portSlot}/";

    public Host(int portSlot)
    {
        ServiceMode = portSlot < 0
            ? RpcServiceMode.Client
            : UseHybridServiceSampler.Next()
                ? RpcServiceMode.Hybrid
                : RpcServiceMode.ServerAndClient;
        UseRemoteComputedCache = ServiceMode == RpcServiceMode.Client
            || UseRemoteComputedCacheSampler.Next();

        Id = $"{ServiceMode:G}{(UseRemoteComputedCache ? "+Cache" : "")}-{Interlocked.Increment(ref _lastId)}:{portSlot}";
        Ref = new HostRef(Id);
        Hash = Random.Shared.Next();
        PortSlot = portSlot;
        Url = GetUrl(portSlot);

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders().AddDebug();

        var services = builder.Services;

        // Fusion & RPC setup
        var fusion = services.AddFusion();
        fusion.AddWebServer();
        services.AddSingleton<RpcHelpers>();
        fusion.Rpc.AddWebSocketClient(c => {
            var rpcHelpers = c.GetRequiredService<RpcHelpers>();
            return new RpcWebSocketClient.Options() {
                HostUrlResolver = rpcHelpers.GetHostUrl,
            };
        });
        services.AddSingleton<RpcCallRouter>(c => c.GetRequiredService<RpcHelpers>().RouteCall);
        if (UseRemoteComputedCache)
            fusion.AddSharedRemoteComputedCache<InMemoryRemoteComputedCache, InMemoryRemoteComputedCache.Options>(
                _ => InMemoryRemoteComputedCache.Options.Default);

        // Actual services
        services.AddSingleton(_ => this);
        fusion.Rpc.AddService<ISimpleCounter, SimpleCounter>(ServiceMode);
        fusion.Commander.AddHandlers<ISimpleCounter>();
        fusion.AddService<IFusionCounter, FusionCounter>(ServiceMode);
        services.AddHostedService<MeshStateUpdater>();
        services.AddHostedService<LifetimeController>();
        services.AddHostedService<TestRunner>();
        var app = builder.Build();

        app.UseWebSockets();
        app.MapRpcWebSocketServer();
        App = app;
    }

    public override string ToString()
        => Id.Value;

    public void RequestStop()
    {
        try {
            Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        }
        catch (ObjectDisposedException) {
            // Already stopping
        }
    }

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        try {
            var runTask = App.RunAsync(Url).WaitAsync(cancellationToken);
            await runTask.WaitAsync(cancellationToken).SilentAwait(false);
            if (!runTask.IsCompleted) { // cancellationToken is cancelled
                RequestStop();
                await runTask.ConfigureAwait(false);
            }
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested) {
            await Console.Error.WriteLineAsync($"{this} failed: {e.Message}".PastelBg(ConsoleColor.DarkRed));
        }
        finally {
            MeshState.Unregister(this);
        }
    }
}
