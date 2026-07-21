using ActualLab.OS;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc;

/// <summary>
/// Central hub that manages RPC peers, services, and configuration for the RPC infrastructure.
/// </summary>
public sealed class RpcHub : ProcessorBase, IHasServices, IHasId<Guid>
{
    internal readonly RpcRegistryOptions RegistryOptions;
    internal readonly RpcPeerOptions PeerOptions;
    internal readonly RpcInboundCallOptions InboundCallOptions;
    internal readonly RpcOutboundCallOptions OutboundCallOptions;
    internal readonly RpcDiagnosticsOptions DiagnosticsOptions;
    internal readonly RpcClientPeerReconnectDelayer ClientPeerReconnectDelayer;
    internal readonly IRpcMiddleware[] Middlewares;
    internal RpcSystemCallSender SystemCallSender => field ??= Services.GetRequiredService<RpcSystemCallSender>();
    internal RpcClient Client => field ??= Services.GetRequiredService<RpcClient>();

    internal ConcurrentDictionary<RpcRoute, RpcPeer> Peers { get; } = new(HardwareInfo.ProcessorCountPo2, 17);

    public Guid Id { get; init; } = Guid.NewGuid();
    public HostId HostId => field ??= Services.GetRequiredService<HostId>();
    public IServiceProvider Services { get; }
    public RpcConfiguration Configuration { get; }
    public RpcServiceRegistry ServiceRegistry => field ??= Services.GetRequiredService<RpcServiceRegistry>();
    public RpcSerializationFormatResolver SerializationFormats { get; }
    public RpcInternalServices InternalServices { get; }
    public RpcLimits Limits { get; }
    public MomentClock SystemClock { get; }

    // The most useful peers are cached
    public RpcClientPeer DefaultPeer => field ??= (RpcClientPeer)GetPeer(RpcRef.Default);
    public RpcClientPeer LoopbackPeer => field ??= (RpcClientPeer)GetPeer(RpcRef.Loopback);
    public RpcClientPeer LocalPeer => field ??= (RpcClientPeer)GetPeer(RpcRef.Local);
    public RpcClientPeer NonePeer => field ??= (RpcClientPeer)GetPeer(RpcRef.None);

    public RpcHub(IServiceProvider services)
    {
        Services = services;

        // Configuration
        Configuration = services.GetRequiredService<RpcConfiguration>();
        Configuration.Freeze();

        // Services
        RegistryOptions = services.GetRequiredService<RpcRegistryOptions>();
        PeerOptions = services.GetRequiredService<RpcPeerOptions>();
        InboundCallOptions = services.GetRequiredService<RpcInboundCallOptions>();
        OutboundCallOptions = services.GetRequiredService<RpcOutboundCallOptions>();
        DiagnosticsOptions = services.GetRequiredService<RpcDiagnosticsOptions>();
        SerializationFormats = services.GetRequiredService<RpcSerializationFormatResolver>();
        Middlewares = services.GetServices<IRpcMiddleware>().OrderByDescending(x => x.Priority).ToArray();
        ClientPeerReconnectDelayer = services.GetRequiredService<RpcClientPeerReconnectDelayer>();
        Limits = services.GetRequiredService<RpcLimits>();
        SystemClock = services.Clocks().SystemClock;
        InternalServices = new(this); // Must go at last
    }

    protected override Task DisposeAsyncCore()
    {
        var disposeTasks = new List<Task>();
        foreach (var (_, peer) in Peers)
            disposeTasks.Add(peer.DisposeAsync().AsTask());
        return Task.WhenAll(disposeTasks);
    }

    // GetClient

    public TService GetClient<TService>()
        => (TService)GetClient(typeof(TService));

    public object GetClient(Type serviceType)
    {
        var serviceDef = ServiceRegistry.Get(serviceType);
        if (serviceDef is null)
            throw ActualLab.Rpc.Internal.Errors.NoService(serviceType);

        var client = serviceDef.Client;
        return client ?? throw ActualLab.Rpc.Internal.Errors.NoClient(serviceDef);
    }

    // GetServer

    public TService GetServer<TService>()
        => (TService)GetServer(typeof(TService));

    public object GetServer(Type serviceType)
    {
        var serviceDef = ServiceRegistry.Get(serviceType);
        if (serviceDef is null)
            throw ActualLab.Rpc.Internal.Errors.NoService(serviceType);

        var client = serviceDef.Server;
        return client ?? throw ActualLab.Rpc.Internal.Errors.NoServer(serviceDef);
    }

    // Peer management

    public RpcClientPeer GetClientPeer(RpcRef rpcRef)
        => (RpcClientPeer)GetPeer(rpcRef.RequireClient());

    public RpcServerPeer GetServerPeer(RpcRef rpcRef)
        => (RpcServerPeer)GetPeer(rpcRef.RequireServer());

    public RpcPeer GetPeer(RpcRef rpcRef)
        => GetPeer(rpcRef.Route); // Re-mints the route if the current one is changed

    public RpcClientPeer GetClientPeer(RpcRoute route)
    {
        route.Ref.RequireClient();
        return (RpcClientPeer)GetPeer(route);
    }

    public RpcServerPeer GetServerPeer(RpcRoute route)
    {
        route.Ref.RequireServer();
        return (RpcServerPeer)GetPeer(route);
    }

    public RpcPeer GetPeer(RpcRoute route)
    {
        // This method uses the exact route it gets: a peer created for an already changed
        // route disposes itself instantly, and its calls reroute - so this is safe.
        if (Peers.TryGetValue(route, out var peer))
            return peer;

        lock (Lock) {
            if (Peers.TryGetValue(route, out peer))
                return peer;
            if (WhenDisposed is not null)
                throw Errors.AlreadyDisposed(GetType());

            peer = PeerOptions.PeerFactory.Invoke(this, route);
            Peers[route] = peer; // One entry per route generation; a drained generation's entry is removed via RemovePeer
            peer.Start(isolate: true); // We don't want to capture Activity.Current, etc. here
            if (peer.Route is { IsStatic: false } peerRoute)
                _ = peerRoute.WhenChanged.ContinueWith(_ => {
                    peer.Dispose();
                    peer.Log.LogWarning("'{Route}': Route is changed, peer {Peer} is disposed", peer.Route, peer);
                }, TaskScheduler.Default);
            return peer;
        }
    }

    internal bool RemovePeer(RpcPeer peer)
    {
        if (!Peers.TryRemove(peer.Route, peer))
            return false;

        peer.Log.LogWarning("'{Route}': peer is removed from RpcHub", peer.Route);
        return true;
    }
}
