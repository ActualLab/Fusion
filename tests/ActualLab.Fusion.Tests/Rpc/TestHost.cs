using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Server;
using ActualLab.Fusion.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace ActualLab.Fusion.Tests.Rpc;

public sealed class TestHost : IAsyncDisposable
{
    private static int _lastId;

    public string Id { get; }
    public int Port { get; }
    public string Url { get; }
    public WebApplication App { get; }
    public IServiceProvider Services => App.Services;
    public RpcServiceMode ServiceMode { get; }

    public static string GetUrl(int port)
        => $"http://localhost:{port}/";

    public TestHost(
        int port,
        RpcServiceMode serviceMode,
        Action<IServiceCollection>? configureServices = null)
    {
        Id = $"{serviceMode:G}-{Interlocked.Increment(ref _lastId)}";
        Port = port;
        ServiceMode = serviceMode;
        Url = GetUrl(port);

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();

        var services = builder.Services;

        // Fusion & RPC setup
        var fusion = services.AddFusion();
        if (port >= 0)
            fusion.AddWebServer();
        services.AddSingleton(_ => this);

        // Setup RPC client
        fusion.Rpc.AddWebSocketClient(c => {
            return new RpcWebSocketClient.Options() {
                HostUrlResolver = GetHostUrl,
            };
        });

        // Setup routing
        services.AddSingleton<RpcCallRouter>(c => RouteCall);
        services.AddSingleton<RpcPeerConnectionKindResolver>(c => GetPeerConnectionKind);

        // Allow custom service configuration
        configureServices?.Invoke(services);

        var app = builder.Build();
        app.UseWebSockets();
        app.MapRpcWebSocketServer();
        App = app;
    }

    public override string ToString()
        => Id;

    public async Task Start(CancellationToken cancellationToken = default)
    {
        TestMeshState.Register(this);
        await App.StartAsync(cancellationToken);
    }

    public async Task Stop(CancellationToken cancellationToken = default)
    {
        TestMeshState.Unregister(this);
        await App.StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        TestMeshState.Unregister(this);
        await App.DisposeAsync();
    }

    private string GetHostUrl(RpcWebSocketClient client, RpcClientPeer peer)
    {
        if (peer.Ref is not RpcTestPeerRef testPeerRef)
            return "";

        var host = TestMeshState.State.Value.HostById.GetValueOrDefault(testPeerRef.HostId);
        return host?.Url ?? "";
    }

    private RpcPeerRef RouteCall(RpcMethodDef method, ArgumentList arguments)
    {
        if (method.IsCommand && Invalidation.IsActive)
            return RpcPeerRef.Local;

        // For testing, we route based on first string argument being a HostId
        if (arguments.Length > 0 && arguments.GetType(0) == typeof(string)) {
            var hostId = arguments.Get<string>(0);
            if (!string.IsNullOrEmpty(hostId))
                return RpcTestPeerRef.Get(hostId);
        }

        return RpcPeerRef.Local;
    }

    private RpcPeerConnectionKind GetPeerConnectionKind(RpcHub hub, RpcPeerRef peerRef)
    {
        var connectionKind = peerRef.ConnectionKind;
        var hostId = peerRef switch {
            RpcTestPeerRef testPeerRef => testPeerRef.HostId,
            _ => null
        };
        if (hostId is null || connectionKind is not RpcPeerConnectionKind.Remote)
            return connectionKind;

        return hostId == Id
            ? RpcPeerConnectionKind.Local
            : RpcPeerConnectionKind.Remote;
    }
}
