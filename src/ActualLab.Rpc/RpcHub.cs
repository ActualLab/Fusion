using System.Diagnostics.CodeAnalysis;
using ActualLab.OS;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc;

public sealed class RpcHub : ProcessorBase, IHasServices, IHasId<Guid>
{
    internal readonly RpcRegistryOptions RegistryOptions;
    internal readonly RpcPeerOptions PeerOptions;
    internal readonly RpcInboundCallOptions InboundCallOptions;
    internal readonly RpcOutboundCallOptions OutboundCallOptions;
    internal readonly RpcWebSocketClientOptions WebSocketClientOptions;
    internal readonly RpcDiagnosticsOptions DiagnosticsOptions;
    internal readonly RpcClientPeerReconnectDelayer ClientPeerReconnectDelayer;
    internal readonly IRpcInboundCallPreprocessor[] InboundCallPreprocessorFactories;
    [field: AllowNull, MaybeNull]
    internal RpcSystemCallSender SystemCallSender => field ??= Services.GetRequiredService<RpcSystemCallSender>();
    [field: AllowNull, MaybeNull]
    internal RpcClient Client => field ??= Services.GetRequiredService<RpcClient>();

    internal ConcurrentDictionary<RpcPeerRef, RpcPeer> Peers { get; } = new(HardwareInfo.ProcessorCountPo2, 17);

    public Guid Id { get; init; } = Guid.NewGuid();
    public TimeSpan PeerRemoveDelay { get; init; } = TimeSpan.FromMinutes(5);
    [field: AllowNull, MaybeNull]
    public HostId HostId => field ??= Services.GetRequiredService<HostId>();
    public IServiceProvider Services { get; }
    public RpcConfiguration Configuration { get; }
    [field: AllowNull, MaybeNull]
    public RpcServiceRegistry ServiceRegistry => field ??= Services.GetRequiredService<RpcServiceRegistry>();
    public RpcSerializationFormatResolver SerializationFormats { get; }
    public RpcInternalServices InternalServices { get; }
    public RpcLimits Limits { get; }
    public MomentClock Clock { get; }

    // The most useful peers are cached
    [field: AllowNull, MaybeNull]
    public RpcClientPeer DefaultPeer => field ??= (RpcClientPeer)GetPeer(RpcPeerRef.Default);
    [field: AllowNull, MaybeNull]
    public RpcClientPeer LoopbackPeer => field ??= (RpcClientPeer)GetPeer(RpcPeerRef.Loopback);
    [field: AllowNull, MaybeNull]
    public RpcClientPeer LocalPeer => field ??= (RpcClientPeer)GetPeer(RpcPeerRef.Local);
    [field: AllowNull, MaybeNull]
    public RpcClientPeer NonePeer => field ??= (RpcClientPeer)GetPeer(RpcPeerRef.None);

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
        WebSocketClientOptions = services.GetRequiredService<RpcWebSocketClientOptions>();
        DiagnosticsOptions = services.GetRequiredService<RpcDiagnosticsOptions>();
        SerializationFormats = services.GetRequiredService<RpcSerializationFormatResolver>();
        InboundCallPreprocessorFactories = services.GetServices<IRpcInboundCallPreprocessor>().ToArray();
        ClientPeerReconnectDelayer = services.GetRequiredService<RpcClientPeerReconnectDelayer>();
        Limits = services.GetRequiredService<RpcLimits>();
        Clock = services.Clocks().CpuClock;
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

    public RpcPeer GetPeer(RpcPeerRef peerRef)
    {
        if (Peers.TryGetValue(peerRef, out var peer))
            return peer;

        lock (Lock) {
            if (Peers.TryGetValue(peerRef, out peer))
                return peer;
            if (WhenDisposed is not null)
                throw Errors.AlreadyDisposed(GetType());

            peer = PeerOptions.CreatePeer(this, peerRef);
            Peers[peerRef] = peer;
            peer.Start(isolate: true); // We don't want to capture Activity.Current, etc. here
            if (peerRef.CanBeRerouted)
                _ = peerRef.WhenRerouted().ContinueWith(_ => {
                    peer.Dispose();
                    peer.Log.LogWarning("'{PeerRef}': Ref is rerouted, peer {Peer} is disposed", peer.Ref, peer);
                }, TaskScheduler.Default);
            return peer;
        }
    }

    public RpcClientPeer GetClientPeer(RpcPeerRef peerRef)
        => (RpcClientPeer)GetPeer(peerRef.RequireClient());

    public RpcServerPeer GetServerPeer(RpcPeerRef peerRef)
        => (RpcServerPeer)GetPeer(peerRef.RequireServer());

    // You normally shouldn't call this method
    public bool RemovePeer(RpcPeer peer)
    {
        if (!Peers.TryRemove(peer.Ref, peer))
            return false;

        peer.Log.LogWarning("'{PeerRef}': peer is removed from RpcHub", peer.Ref);
        return true;
    }
}
