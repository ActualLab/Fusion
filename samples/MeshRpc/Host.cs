using ActualLab.Async;
using ActualLab.Fusion;
using ActualLab.Fusion.Server;
using ActualLab.Generators;
using ActualLab.Mathematics;
using ActualLab.Rpc;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Server;
using ActualLab.Text;
using ActualLab.Time;
using Pastel;
using Samples.MeshRpc.Services;

namespace Samples.MeshRpc;

public sealed class Host : WorkerBase
{
    private static int _lastId;

    public Symbol Id { get; }
    public HostRef Ref { get; }
    public int Hash { get; }
    public int PortOffset { get; }
    public string Url { get; }
    public RandomTimeSpan Delay { get; } = TimeSpan.FromMilliseconds(100).ToRandom(0.5);
    public WebApplication App { get; }
    public IServiceProvider Services => App.Services;

    public static string GetUrl(int slot)
        => $"http://localhost:{22222 + slot}/";

    public Host(int portOffset)
    {
        var serviceMode = RandomShared.NextDouble() < 0.5 ? RpcServiceMode.Hybrid : RpcServiceMode.Server;
        Id = $"{serviceMode:G}-{Interlocked.Increment(ref _lastId)}:{portOffset}";
        Ref = new HostRef(Id);
        Hash = Random.Shared.Next();
        PortOffset = portOffset;
        Url = GetUrl(portOffset);

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders().AddDebug();

        var services = builder.Services;
        services.AddSingleton(_ => this);
        var fusion = services.AddFusion();
        fusion.AddWebServer();
        fusion.Rpc.AddWebSocketClient(c => {
            var rpcHelpers = c.GetRequiredService<RpcHelpers>();
            return new RpcWebSocketClient.Options() {
                HostUrlResolver = rpcHelpers.GetHostUrl,
            };
        });
        services.AddSingleton<RpcCallRouter>(c => c.GetRequiredService<RpcHelpers>().RouteCall);
        fusion.Rpc.AddService<ICounter, Counter>(serviceMode);
        fusion.AddService<IFusionCounter, FusionCounter>(serviceMode);
        services.AddHostedService<Tester>();
        services.AddHostedService<HostKiller>();
        var app = builder.Build();

        app.UseWebSockets();
        app.MapRpcWebSocketServer();
        App = app;
    }

    public void RequestStop()
        => Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        var applicationLifetime = Services.GetRequiredService<IHostApplicationLifetime>();
        try {
            var runTask = App.RunAsync(Url).WaitAsync(cancellationToken);
            await TaskExt.NewNeverEndingUnreferenced().WaitAsync(applicationLifetime.ApplicationStarted).SilentAwait(false);
            MeshState.Register(this);

            await runTask.WaitAsync(cancellationToken).SilentAwait(false);
            if (!runTask.IsCompleted) { // cancellationToken is cancelled
                RequestStop();
                await runTask.ConfigureAwait(false);
            }
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested) {
            await Console.Error.WriteLineAsync($"Server failed: {e.Message}".PastelBg(ConsoleColor.DarkRed));
        }
        finally {
            MeshState.Unregister(this);
        }
    }
}
