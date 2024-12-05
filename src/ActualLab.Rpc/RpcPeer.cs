using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc;

public abstract class RpcPeer : WorkerBase, IHasId<Guid>
{
    public static LogLevel DefaultCallLogLevel { get; set; } = LogLevel.None;

    private volatile AsyncState<RpcPeerConnectionState> _connectionState = new(RpcPeerConnectionState.Disconnected, true);
    private volatile RpcHandshake? _handshake;
    private volatile RpcMethodResolver _serverMethodResolver;
    private volatile ChannelWriter<RpcMessage>? _sender;
    private volatile RpcPeerStopMode _stopMode;
    private bool _resetTryIndex;

    protected IServiceProvider Services => Hub.Services;

    protected internal ChannelWriter<RpcMessage>? Sender {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _sender ?? _connectionState.Value.Sender; // _sender is set after _connectionState, so can be out of sync
    }

    [field: AllowNull, MaybeNull]
    protected internal RpcCallLogger CallLogger
        => field ??= Hub.CallLoggerFactory.Invoke(this, Hub.CallLoggerFilter, Log, CallLogLevel);
    [field: AllowNull, MaybeNull]
    protected internal ILogger Log
        => field ??= Services.LogFor(GetType());

    public RpcHub Hub { get; }
    public RpcPeerRef Ref { get; }
    public Guid Id { get; } = Guid.NewGuid();
    public CpuTimestamp CreatedAt { get; } = CpuTimestamp.Now;
    public RpcPeerConnectionKind ConnectionKind { get; }
    public VersionSet Versions { get; init; }
    public RpcSerializationFormat SerializationFormat { get; init; }
    public RpcArgumentSerializer ArgumentSerializer { get; init; }
    public RpcHashProvider HashProvider { get; init; }
    public RpcInboundContextFactory InboundContextFactory { get; init; }
    public RpcInboundCallFilter InboundCallFilter { get; init; }
    public RpcInboundCallTracker InboundCalls { get; init; }
    public RpcOutboundCallTracker OutboundCalls { get; init; }
    public RpcRemoteObjectTracker RemoteObjects { get; init; }
    public RpcSharedObjectTracker SharedObjects { get; init; }
    public RpcPeerInternalServices InternalServices => new(this);
    public LogLevel CallLogLevel { get; init; } = DefaultCallLogLevel;

    public AsyncState<RpcPeerConnectionState> ConnectionState => _connectionState;
    public RpcMethodResolver ServerMethodResolver => _serverMethodResolver;
    public RpcHandshake? Handshake => _handshake;

    public RpcPeerStopMode StopMode {
        get => _stopMode;
        set {
            lock (Lock)
                _stopMode = value;
        }
    }

    protected RpcPeer(RpcHub hub, RpcPeerRef peerRef, VersionSet? versions)
    {
        // ServiceRegistry is resolved in lazy fashion in RpcHub.
        // We access it here to make sure any configuration error gets thrown at this point.
        _ = hub.ServiceRegistry;

        Hub = hub;
        Ref = peerRef;
        ConnectionKind = peerRef.GetConnectionKind(hub);
        Versions = versions ?? peerRef.GetVersions();
        _serverMethodResolver = GetServerMethodResolver(null);

        SerializationFormat = Hub.SerializationFormats.Get(peerRef);
        ArgumentSerializer = SerializationFormat.ArgumentSerializer;
        HashProvider = Hub.HashProvider;
        InboundContextFactory = Hub.InboundContextFactory;
        InboundCallFilter = Hub.InboundCallFilter;

        var services = hub.Services;
        InboundCalls = services.GetRequiredService<RpcInboundCallTracker>();
        InboundCalls.Initialize(this);
        OutboundCalls = services.GetRequiredService<RpcOutboundCallTracker>();
        OutboundCalls.Initialize(this);
        RemoteObjects = services.GetRequiredService<RpcRemoteObjectTracker>();
        RemoteObjects.Initialize(this);
        SharedObjects = services.GetRequiredService<RpcSharedObjectTracker>();
        SharedObjects.Initialize(this);
    }

    public override string ToString()
        => $"{GetType().Name}({Ref}, #{GetHashCode()})";

    public Task Send(RpcMessage message, ChannelWriter<RpcMessage>? sender)
    {
        // !!! Send should never throw an exception.
        // This method is optimized to run as quickly as possible,
        // that's why it is a bit complicated.

        sender ??= Sender;
        try {
            return sender == null || sender.TryWrite(message)
                ? Task.CompletedTask
                : CompleteAsync(this, message, sender);
        }
        catch (Exception e) {
            Log.LogError(e, "Send failed");
            return Task.CompletedTask;
        }

        static async Task CompleteAsync(RpcPeer peer, RpcMessage message, ChannelWriter<RpcMessage> sender) {
            // !!! This method should never fail
            try {
                // If we're here, WaitToWriteAsync call is required to continue
                while (await sender.WaitToWriteAsync(peer.StopToken).ConfigureAwait(false))
                    if (sender.TryWrite(message))
                        return;

                throw new ChannelClosedException();
            }
            catch (Exception e) when (!e.IsCancellationOf(peer.StopToken)) {
                peer.Log.LogError(e, "Send failed");
            }
        }
    }

    public bool IsConnected()
        => ConnectionState.Value.Handshake != null;

    public bool IsConnected(
        [NotNullWhen(true)] out RpcHandshake? handshake,
        [NotNullWhen(true)] out ChannelWriter<RpcMessage>? sender)
    {
        var connectionState = ConnectionState.Value;
        handshake = connectionState.Handshake;
        sender = connectionState.Sender;
        return handshake != null;
    }

    public async Task<(RpcHandshake Handshake, ChannelWriter<RpcMessage> Sender)> WhenConnected(
        CancellationToken cancellationToken = default)
    {
        var connectionState = ConnectionState;
        while (true) {
            try {
                connectionState = await connectionState.WhenConnected(cancellationToken).ConfigureAwait(false);
                var vConnectionState = connectionState.Value;
                if (vConnectionState.Handshake is not { } handshake)
                    continue;
                if (vConnectionState.Sender is not { } sender)
                    continue;

                var spinWait = new SpinWait();
                while (!connectionState.HasNext) {
                    // Waiting for _sender assignment
                    if (ReferenceEquals(sender, _sender))
                        return (handshake, sender);

                    spinWait.SpinOnce();
                }
            }
            catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                if (!ConnectionState.IsFinal)
                    continue;
                if (Ref.IsRerouted)
                    throw RpcRerouteException.MustReroute();
                throw;
            }
        }
    }

    public Task<(RpcHandshake Handshake, ChannelWriter<RpcMessage> Sender)> WhenConnected(
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return timeout == TimeSpan.MaxValue
            ? WhenConnected(cancellationToken)
            : WhenConnectedWithTimeout(this, timeout, cancellationToken);

        static async Task<(RpcHandshake Handshake, ChannelWriter<RpcMessage> Sender)> WhenConnectedWithTimeout(
            RpcPeer peer, TimeSpan timeout1, CancellationToken cancellationToken1)
        {
            using var timeoutCts = cancellationToken1.CreateLinkedTokenSource(timeout1);
            var timeoutToken = timeoutCts.Token;
            try {
                return await peer.WhenConnected(timeoutToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutToken.IsCancellationRequested) {
                if (cancellationToken1.IsCancellationRequested)
                    throw; // Not a timeout

                throw Errors.ConnectTimeout(peer.Ref);
            }
        }
    }

    public Task Disconnect(
        bool abortReader = false,
        Exception? error = null,
        AsyncState<RpcPeerConnectionState>? expectedState = null)
    {
        AsyncState<RpcPeerConnectionState> connectionState;
        CancellationTokenSource? readerTokenSource;
        ChannelWriter<RpcMessage>? sender;
        lock (Lock) {
            // We want to make sure ConnectionState doesn't change while this method runs
            // and no one else cancels ReaderTokenSource
            connectionState = _connectionState;
            if (expectedState != null && expectedState != connectionState)
                return Task.CompletedTask;
            if (connectionState.IsFinal || !connectionState.Value.IsConnected())
                return Task.CompletedTask;

            sender = _sender;
            readerTokenSource = connectionState.Value.ReaderTokenSource;
            _sender = null;
        }
        sender?.TryComplete(error);
        if (abortReader)
            readerTokenSource.CancelAndDisposeSilently();
        // ReSharper disable once MethodSupportsCancellation
        return connectionState.WhenNext();
    }

    public void ResetTryIndex()
    {
        lock (Lock)
            _resetTryIndex = true;
    }

    // Protected methods

    protected abstract Task<RpcConnection> GetConnection(
        RpcPeerConnectionState connectionState,
        CancellationToken cancellationToken);

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        if (ConnectionKind == RpcPeerConnectionKind.Local) {
            // It's a fake RpcPeer that exists solely to be "available"
            await TaskExt.NewNeverEndingUnreferenced().WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        Log.LogInformation("'{PeerRef}': Started", Ref);
        foreach (var peerTracker in Hub.PeerTrackers)
            peerTracker.Invoke(this);

        var handshakeIndex = 0;
        var connectionState = ConnectionState;
        var lastHandshake = (RpcHandshake?)null;
        var peerChangedCts = cancellationToken.CreateLinkedTokenSource();
        var peerChangedToken = peerChangedCts.Token;
        try {
            while (true) {
                var error = (Exception?)null;
                var readerToken = CancellationToken.None;
                try {
                    if (connectionState.IsFinal)
                        return;

                    if (connectionState.Value.IsConnected())
                        connectionState = SetConnectionState(connectionState.Value.NextDisconnected(), connectionState).RequireNonFinal();

                    var connection = await GetConnection(connectionState.Value, cancellationToken).ConfigureAwait(false);
                    var channel = connection.Channel;
                    var sender = channel.Writer;
                    var reader = channel.Reader;

                    // Sending Handshake call
                    using var handshakeCts = cancellationToken.CreateLinkedTokenSource(Hub.Limits.HandshakeTimeout);
                    var handshakeToken = handshakeCts.Token;
                    RpcHandshake handshake;
                    try {
                        handshake = await Task.Run(
                            async () => {
                                var ownHandshake = new RpcHandshake(
                                    Id, Versions, Hub.Id,
                                    RpcHandshake.CurrentProtocolVersion,
                                    ++handshakeIndex);
                                await Hub.SystemCallSender
                                    .Handshake(this, sender, ownHandshake)
                                    .ConfigureAwait(false);
                                var message = await reader.ReadAsync(handshakeToken).ConfigureAwait(false);
                                var handshakeContext = ProcessMessage(message, handshakeToken, handshakeToken);
                                var remoteHandshake = handshakeContext?.Call.Arguments?.GetUntyped(0) as RpcHandshake;
                                return remoteHandshake ?? throw Errors.HandshakeFailed();
                            }, handshakeToken)
                            .WaitAsync(handshakeToken)
                            .ConfigureAwait(false);
                        if (handshake.RemoteApiVersionSet == null)
                            handshake = handshake with { RemoteApiVersionSet = new() };
                    }
                    catch (OperationCanceledException) {
                        if (!cancellationToken.IsCancellationRequested && handshakeToken.IsCancellationRequested)
                            throw Errors.HandshakeTimeout();
                        throw;
                    }

                    // Processing Handshake
                    var peerChangeKind = handshake.GetPeerChangeKind(lastHandshake);
                    lastHandshake = handshake;
                    if (peerChangeKind != RpcPeerChangeKind.Unchanged) {
                        // Remote RpcPeer changed -> we must abort every inbound call / shared object
                        if (peerChangeKind != RpcPeerChangeKind.ChangedToVeryFirst) {
                            peerChangedCts.CancelAndDisposeSilently();
                            peerChangedCts = cancellationToken.CreateLinkedTokenSource();
                            peerChangedToken = peerChangedCts.Token;
                            await Reset(Errors.PeerChanged()).ConfigureAwait(false);
                        }
                    }

                    // Only at this point: expose the new connection state
                    var readerTokenSource = cancellationToken.CreateLinkedTokenSource();
                    readerToken = readerTokenSource.Token;
                    var nextConnectionState = connectionState.Value.NextConnected(connection, handshake, readerTokenSource);
                    connectionState = SetConnectionState(nextConnectionState, connectionState).RequireNonFinal();
                    if (connectionState.Value.Connection != connection)
                        continue; // Somehow disconnected

                    _ = Task.Run(() => {
                        var tasks = new List<Task> {
                            SharedObjects.Maintain(handshake, readerToken),
                            RemoteObjects.Maintain(handshake, readerToken),
                            OutboundCalls.Maintain(handshake, readerToken)
                        };
                        if (peerChangeKind != RpcPeerChangeKind.ChangedToVeryFirst) {
                            var isPeerChanged = peerChangeKind == RpcPeerChangeKind.Changed;
                            tasks.Add(OutboundCalls.Reconnect(handshake, isPeerChanged, readerToken));
                        }
                        return Task.WhenAll(tasks);
                    }, readerToken);

                    while (await reader.WaitToReadAsync(readerToken).ConfigureAwait(false))
                    while (reader.TryRead(out var message))
                        _ = ProcessMessage(message, peerChangedToken, readerToken);
                }
                catch (Exception e) {
                    var isReaderAbort = readerToken.IsCancellationRequested
                        && !cancellationToken.IsCancellationRequested;
                    error = isReaderAbort ? null : e;
                }
                if (cancellationToken.IsCancellationRequested) {
                    var isTerminal = error != null && Hub.PeerTerminalErrorDetector.Invoke(error);
                    if (!isTerminal)
                        error = RpcReconnectFailedException.StopRequested(error);
                }
                connectionState = SetConnectionState(connectionState.Value.NextDisconnected(error));
                if (!connectionState.IsFinal)
                    continue;

                if (Ref.IsRerouted)
                    OutboundCalls.TryReroute();
                break;
            }
        }
        finally {
            Log.LogInformation("'{PeerRef}': Stopping", Ref);
            Hub.Peers.TryRemove(Ref, this);

            // Make sure the sequence of ConnectionStates terminates
            Exception? error;
            lock (Lock) {
                connectionState = _connectionState;
                error = connectionState.Value.Error;
                if (!connectionState.IsFinal) {
                    var isTerminal = error != null && Hub.PeerTerminalErrorDetector.Invoke(error);
                    if (!isTerminal)
                        error = new RpcReconnectFailedException(error);
                    SetConnectionState(connectionState.Value.NextDisconnected(error));
                }
                else if (error == null) {
                    Log.LogError("The final connection state must have a non-null Error");
                    error = RpcReconnectFailedException.Unspecified();
                }
            }

            peerChangedCts.CancelAndDisposeSilently(); // Terminates all running ProcessMessage calls
            await Reset(error!, true).ConfigureAwait(false);
        }
    }

    protected async Task Reset(Exception error, bool isStopped = false)
    {
        RemoteObjects.Abort();
        await SharedObjects.Abort(error).ConfigureAwait(false);
        if (isStopped)
            await OutboundCalls.Abort(error, assumeCancelled: true).ConfigureAwait(false);
        // Inbound calls are auto-aborted via peerChangedToken from OnRun,
        // which becomes RpcInboundCallContext.CancellationToken.
        InboundCalls.Clear();
        Log.LogInformation("'{PeerRef}': {Action}", Ref, isStopped ? "Stopped" : "Peer changed");
    }

    protected RpcInboundContext? ProcessMessage(
        RpcMessage message,
        CancellationToken peerChangedToken,
        CancellationToken cancellationToken)
    {
        try {
            var context = InboundContextFactory.Invoke(this, message, peerChangedToken);
            using var scope = context.Activate();
            _ = context.Call.Process(cancellationToken);
            return context;
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            Log.LogError(e, "Failed to process message: {Message}", message);
            return null;
        }
    }

    protected AsyncState<RpcPeerConnectionState> SetConnectionState(
        RpcPeerConnectionState newState,
        AsyncState<RpcPeerConnectionState>? expectedState = null)
    {
#if NET9_0_OR_GREATER
        Lock.Enter();
#else
        Monitor.Enter(Lock);
#endif
        var connectionState = _connectionState;
        var oldState = connectionState.Value;
        if ((expectedState != null && connectionState != expectedState) || ReferenceEquals(newState, oldState)) {
#if NET9_0_OR_GREATER
            Lock.Exit();
#else
            Monitor.Exit(Lock);
#endif
            return connectionState;
        }
        Exception? terminalError = null;
        try {
            if (newState.TryIndex != 0 && _resetTryIndex) {
                _resetTryIndex = false;
                newState = newState with { TryIndex = 0 };
            }
            var nextConnectionState = connectionState.TrySetNext(newState);
            if (ReferenceEquals(nextConnectionState, connectionState)) {
#if NET9_0_OR_GREATER
                Lock.Exit();
#else
                Monitor.Exit(Lock);
#endif
                return connectionState;
            }
            _connectionState = connectionState = nextConnectionState;
            _serverMethodResolver = GetServerMethodResolver(newState.Handshake);
            _handshake = newState.Handshake;
            if (newState.Error != null && Hub.PeerTerminalErrorDetector.Invoke(newState.Error)) {
                terminalError = newState.Error;
                connectionState.TrySetFinal(terminalError);
            }
            return connectionState;
        }
        finally {
            if (newState.ReaderTokenSource != oldState.ReaderTokenSource) {
                // Cancel the old ReaderTokenSource
                oldState.ReaderTokenSource.CancelAndDisposeSilently();
                _sender = newState.Sender;
            }
            if (newState.Connection != oldState.Connection) {
                // Complete the old Channel
                oldState.Sender?.TryComplete(newState.Error);
            }
#if NET9_0_OR_GREATER
            Lock.Exit();
#else
            Monitor.Exit(Lock);
#endif

            // The code below is responsible solely for logging - all important stuff is already done
            if (terminalError != null)
                Log.LogInformation("'{PeerRef}': Can't (re)connect, will shut down", Ref);
            else if (newState.IsConnected())
                Log.LogInformation("'{PeerRef}': Connected", Ref);
            else {
                var e = newState.Error;
                if (e != null)
                    Log.LogWarning(e, "'{PeerRef}': Disconnected: {ErrorMessage}", Ref, e.Message);
                else
                    Log.LogWarning("'{PeerRef}': Disconnected", Ref);
            }
        }
    }

    protected virtual RpcMethodResolver GetServerMethodResolver(RpcHandshake? handshake)
    {
        try {
            return Hub.ServiceRegistry.GetServerMethodResolver(handshake?.RemoteApiVersionSet);
        }
        catch (Exception e) {
            Log.LogError(e, "[LegacyName] conflict");
            return Hub.ServiceRegistry.ServerMethodResolver;
        }
    }

    protected internal virtual RpcPeerStopMode ComputeAutoStopMode()
        => Ref.IsServer
            ? RpcPeerStopMode.KeepInboundCallsIncomplete // The client will likely reconnect or pick another server
            : RpcPeerStopMode.CancelInboundCalls; // When the client dies, server-to-client calls must be cancelled
}
