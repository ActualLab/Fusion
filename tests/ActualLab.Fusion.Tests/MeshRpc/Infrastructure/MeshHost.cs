using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Server;
using ActualLab.Fusion.Server;
using ActualLab.Rpc.Clients;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ActualLab.Fusion.Tests.MeshRpc;

public sealed class MeshHost : IHasServices, IServiceProvider, IAsyncDisposable
{
    private static int _lastId = 0x1000;

    public MeshMap MeshMap { get; }
    public RpcServiceMode ServiceMode { get; }
    public bool AllowLocalRpcConnectionKind { get; }

    public string Id { get; }
    public int Port { get; }
    public string Url { get; }
    public WebApplication App { get; }
    public IServiceProvider Services { get; }
    public Task WhenStarted { get;}

    public MeshHost(
        MeshMap meshMap,
        RpcServiceMode serviceMode,
        bool allowLocalRpcConnectionKind,
        Action<MeshHost, IServiceCollection>? configureServices)
    {
        MeshMap = meshMap;
        ServiceMode = serviceMode;
        AllowLocalRpcConnectionKind = allowLocalRpcConnectionKind;

        Id = $"host-{Interlocked.Increment(ref _lastId):x4}-{serviceMode:G}";
        Port = WebTestExt.GetUnusedTcpPort();
        Url = $"http://127.0.0.1:{Port}/";

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(Url);
        var services = builder.Services;

        // Shared services
        services.AddSingleton(_ => this);
        services.AddSingleton(meshMap);

        // Fusion & RPC server setup
        var fusion = services.AddFusion();
        fusion.AddWebServer();
        services.AddSingleton<RpcCallRouter>(_ => RouteCall);
        services.AddSingleton<RpcPeerConnectionKindResolver>(_ => GetPeerConnectionKind);

        // Setup RPC client
        fusion.Rpc.AddWebSocketClient(_ => RpcWebSocketClient.Options.Default with {
            HostUrlResolver = GetClientPeerHostUrl,
        });

        // Custom service configuration
        configureServices?.Invoke(this, services);

        var app = builder.Build();
        app.UseWebSockets();
        app.MapRpcWebSocketServer();
        App = app;
        Services = app.Services;
        WhenStarted = app.StartAsync();
    }

    public ValueTask DisposeAsync()
        => App.DisposeAsync();

    public override string ToString()
        => Id;

    public Task Stop(CancellationToken cancellationToken = default)
        => App.StopAsync(cancellationToken);

    public object? GetService(Type serviceType)
        => Services.GetService(serviceType);

    // Private methods

    private RpcPeerRef RouteCall(RpcMethodDef method, ArgumentList arguments)
    {
        if (method.IsCommand && Invalidation.IsActive)
            return RpcPeerRef.Local;

        // For testing, we route based on its argument's hash or value
        if (arguments.Length == 0)
            return RpcPeerRef.Local;

        var arg0 = arguments.Get0Untyped();
        var shardKey = arg0 switch {
            int i => i,
            IHasShardKey hrk => hrk.ShardKey,
            _ => arg0?.GetHashCode() ?? 0
        };
        return MeshMap.GetShardPeerRef(shardKey);
    }

    private RpcPeerConnectionKind GetPeerConnectionKind(RpcHub hub, RpcPeerRef peerRef)
    {
        if (peerRef is not ShardPeerRef testPeerRef)
            return peerRef.ConnectionKind;

        return AllowLocalRpcConnectionKind && testPeerRef.Host == this
            ? RpcPeerConnectionKind.Local
            : RpcPeerConnectionKind.Remote;
    }

    private string GetClientPeerHostUrl(RpcWebSocketClient client, RpcClientPeer peer)
        => peer.Ref is ShardPeerRef testPeerRef
            ? testPeerRef.Host?.Url ?? ""
            : throw new ArgumentOutOfRangeException(nameof(peer));
}
