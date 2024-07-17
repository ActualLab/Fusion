using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.Serialization;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc;

public sealed class RpcHub : ProcessorBase, IHasServices, IHasId<Guid>
{
    private HostId? _hostId;
    private RpcServiceRegistry? _serviceRegistry;
    private IEnumerable<RpcPeerTracker>? _peerTrackers;
    private RpcSystemCallSender? _systemCallSender;
    private RpcClient? _client;

    internal readonly RpcServiceDefBuilder ServiceDefBuilder;
    internal readonly RpcMethodDefBuilder MethodDefBuilder;
    internal readonly RpcBackendServiceDetector BackendServiceDetector;
    internal readonly RpcCommandTypeDetector CommandTypeDetector;
    internal readonly RpcServiceScopeResolver ServiceScopeResolver;
    internal readonly RpcSafeCallRouter CallRouter;
    internal readonly RpcRerouteDelayer RerouteDelayer;
    internal readonly RpcArgumentSerializer ArgumentSerializer;
    internal readonly RpcInboundCallFilter InboundCallFilter;
    internal readonly RpcInboundContextFactory InboundContextFactory;
    internal readonly RpcInboundMiddlewares InboundMiddlewares;
    internal readonly RpcOutboundMiddlewares OutboundMiddlewares;
    internal readonly RpcPeerFactory PeerFactory;
    internal readonly RpcClientConnectionFactory ClientConnectionFactory;
    internal readonly RpcClientPeerReconnectDelayer ClientPeerReconnectDelayer;
    internal readonly RpcPeerTerminalErrorDetector PeerTerminalErrorDetector;
    internal readonly RpcMethodTracerFactory MethodTracerFactory;
    internal readonly RpcCallLoggerFactory CallLoggerFactory;
    internal readonly RpcCallLoggerFilter CallLoggerFilter;
    internal IEnumerable<RpcPeerTracker> PeerTrackers => _peerTrackers ??= Services.GetRequiredService<IEnumerable<RpcPeerTracker>>();
    internal RpcSystemCallSender SystemCallSender => _systemCallSender ??= Services.GetRequiredService<RpcSystemCallSender>();
    internal RpcClient Client => _client ??= Services.GetRequiredService<RpcClient>();

    internal ConcurrentDictionary<RpcPeerRef, RpcPeer> Peers { get; } = new();

    public Guid Id { get; init; } = Guid.NewGuid();
    public HostId HostId => _hostId ??= Services.GetRequiredService<HostId>();
    public IServiceProvider Services { get; }
    public RpcConfiguration Configuration { get; }
    public RpcServiceRegistry ServiceRegistry => _serviceRegistry ??= Services.GetRequiredService<RpcServiceRegistry>();
    public RpcInternalServices InternalServices => new(this);
    public RpcLimits Limits { get; }
    public MomentClock Clock { get; }

    public RpcHub(IServiceProvider services)
    {
        Services = services;
        Configuration = services.GetRequiredService<RpcConfiguration>();
        Configuration.Freeze();

        // Delegates
        ServiceDefBuilder = services.GetRequiredService<RpcServiceDefBuilder>();
        MethodDefBuilder = services.GetRequiredService<RpcMethodDefBuilder>();
        BackendServiceDetector = services.GetRequiredService<RpcBackendServiceDetector>();
        CommandTypeDetector = services.GetRequiredService<RpcCommandTypeDetector>();
        ServiceScopeResolver = services.GetRequiredService<RpcServiceScopeResolver>();
        CallRouter = services.GetRequiredService<RpcSafeCallRouter>();
        RerouteDelayer = services.GetRequiredService<RpcRerouteDelayer>();
        ArgumentSerializer = services.GetRequiredService<RpcArgumentSerializer>();
        InboundCallFilter = services.GetRequiredService<RpcInboundCallFilter>();
        InboundContextFactory = services.GetRequiredService<RpcInboundContextFactory>();
        InboundMiddlewares = services.GetRequiredService<RpcInboundMiddlewares>();
        OutboundMiddlewares = services.GetRequiredService<RpcOutboundMiddlewares>();
        PeerFactory = services.GetRequiredService<RpcPeerFactory>();
        ClientConnectionFactory = services.GetRequiredService<RpcClientConnectionFactory>();
        ClientPeerReconnectDelayer = services.GetRequiredService<RpcClientPeerReconnectDelayer>();
        PeerTerminalErrorDetector = services.GetRequiredService<RpcPeerTerminalErrorDetector>();
        MethodTracerFactory = services.GetRequiredService<RpcMethodTracerFactory>();
        CallLoggerFactory = services.GetRequiredService<RpcCallLoggerFactory>();
        CallLoggerFilter = services.GetRequiredService<RpcCallLoggerFilter>();
        Limits = services.GetRequiredService<RpcLimits>();
        Clock = services.Clocks().CpuClock;
    }

    protected override Task DisposeAsyncCore()
    {
        var disposeTasks = new List<Task>();
        foreach (var (_, peer) in Peers)
            disposeTasks.Add(peer.DisposeAsync().AsTask());
        return Task.WhenAll(disposeTasks);
    }

    public RpcPeer GetPeer(RpcPeerRef peerRef)
    {
        if (Peers.TryGetValue(peerRef, out var peer))
            return peer;

        lock (Lock) {
            if (Peers.TryGetValue(peerRef, out peer))
                return peer;
            if (WhenDisposed != null)
                throw Errors.AlreadyDisposed(GetType());
            if (peerRef.IsRerouted)
                throw RpcRerouteException.MustReroute(peerRef);

            peer = PeerFactory.Invoke(this, peerRef);
            Peers[peerRef] = peer;
            peer.Start();
            if (peerRef.CanBeRerouted)
                _ = peerRef.WhenGone().ContinueWith(_ => peer.Dispose(), TaskScheduler.Default);
            return peer;
        }
    }

    public RpcClientPeer GetClientPeer(RpcPeerRef peerRef)
        => (RpcClientPeer)GetPeer(peerRef.RequireClient());

    public RpcServerPeer GetServerPeer(RpcPeerRef peerRef)
        => (RpcServerPeer)GetPeer(peerRef.RequireServer());
}
