using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.Serialization;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc;

public sealed class RpcHub : ProcessorBase, IHasServices, IHasId<Guid>
{
    private RpcServiceRegistry? _serviceRegistry;
    private IEnumerable<RpcPeerTracker>? _peerTrackers;
    private RpcSystemCallSender? _systemCallSender;
    private RpcClient? _client;

    internal readonly RpcServiceDefBuilder ServiceDefBuilder;
    internal readonly RpcMethodDefBuilder MethodDefBuilder;
    internal readonly RpcServiceScopeResolver ServiceScopeResolver;
    internal readonly RpcCallRouter CallRouter;
    internal readonly RpcArgumentSerializer ArgumentSerializer;
    internal readonly RpcInboundCallFilter InboundCallFilter;
    internal readonly RpcInboundContextFactory InboundContextFactory;
    internal readonly RpcInboundMiddlewares InboundMiddlewares;
    internal readonly RpcOutboundMiddlewares OutboundMiddlewares;
    internal readonly RpcPeerFactory PeerFactory;
    internal readonly RpcClientConnectionFactory ClientConnectionFactory;
    internal readonly RpcClientPeerReconnectDelayer ClientPeerReconnectDelayer;
    internal readonly RpcBackendServiceDetector BackendServiceDetector;
    internal readonly RpcUnrecoverableErrorDetector UnrecoverableErrorDetector;
    internal readonly RpcMethodTracerFactory MethodTracerFactory;
    internal readonly RpcCallLoggerFactory CallLoggerFactory;
    internal readonly RpcCallLoggerFilter CallLoggerFilter;
    internal IEnumerable<RpcPeerTracker> PeerTrackers => _peerTrackers ??= Services.GetRequiredService<IEnumerable<RpcPeerTracker>>();
    internal RpcSystemCallSender SystemCallSender => _systemCallSender ??= Services.GetRequiredService<RpcSystemCallSender>();
    internal RpcClient Client => _client ??= Services.GetRequiredService<RpcClient>();

    internal ConcurrentDictionary<RpcPeerRef, RpcPeer> Peers { get; } = new();

    public IServiceProvider Services { get; }
    public RpcConfiguration Configuration { get; }
    public RpcServiceRegistry ServiceRegistry => _serviceRegistry ??= Services.GetRequiredService<RpcServiceRegistry>();
    public RpcInternalServices InternalServices => new(this);
    public Guid Id { get; init; } = Guid.NewGuid();
    public RpcLimits Limits { get; }
    public IMomentClock Clock { get; }

    public RpcHub(IServiceProvider services)
    {
        Services = services;
        Configuration = services.GetRequiredService<RpcConfiguration>();
        Configuration.Freeze();

        // Delegates
        ServiceDefBuilder = services.GetRequiredService<RpcServiceDefBuilder>();
        MethodDefBuilder = services.GetRequiredService<RpcMethodDefBuilder>();
        ServiceScopeResolver = services.GetRequiredService<RpcServiceScopeResolver>();
        CallRouter = services.GetRequiredService<RpcCallRouter>();
        ArgumentSerializer = services.GetRequiredService<RpcArgumentSerializer>();
        InboundCallFilter = services.GetRequiredService<RpcInboundCallFilter>();
        InboundContextFactory = services.GetRequiredService<RpcInboundContextFactory>();
        InboundMiddlewares = services.GetRequiredService<RpcInboundMiddlewares>();
        OutboundMiddlewares = services.GetRequiredService<RpcOutboundMiddlewares>();
        PeerFactory = services.GetRequiredService<RpcPeerFactory>();
        ClientConnectionFactory = services.GetRequiredService<RpcClientConnectionFactory>();
        ClientPeerReconnectDelayer = services.GetRequiredService<RpcClientPeerReconnectDelayer>();
        BackendServiceDetector = services.GetRequiredService<RpcBackendServiceDetector>();
        UnrecoverableErrorDetector = services.GetRequiredService<RpcUnrecoverableErrorDetector>();
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

            peer = PeerFactory.Invoke(this, peerRef);
            Peers[peerRef] = peer;
            peer.Start();
            return peer;
        }
    }

    public RpcClientPeer GetClientPeer(RpcPeerRef peerRef)
        => (RpcClientPeer)GetPeer(peerRef.RequireClient());

    public RpcServerPeer GetServerPeer(RpcPeerRef peerRef)
        => (RpcServerPeer)GetPeer(peerRef.RequireServer());
}
