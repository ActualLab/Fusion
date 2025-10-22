using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.Serialization;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Rpc;

public abstract class RpcPeer : WorkerBase, IHasId<Guid>
{
    public static LogLevel DefaultCallLogLevel { get; set; } = LogLevel.None;

    private volatile AsyncState<RpcPeerConnectionState> _connectionState = new(RpcPeerConnectionState.Disconnected);
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
    public RpcInboundContextFactory InboundContextFactory { get; init; }
    public RpcInboundCallFilter InboundCallFilter { get; init; }
    public RpcInboundCallTracker InboundCalls { get; init; }
    public RpcOutboundCallTracker OutboundCalls { get; init; }
    public RpcRemoteObjectTracker RemoteObjects { get; init; }
    public RpcSharedObjectTracker SharedObjects { get; init; }
    public RpcPeerInternalServices InternalServices => new(this);
    public LogLevel CallLogLevel { get; init; } = DefaultCallLogLevel;

    public AsyncState<RpcPeerConnectionState> ConnectionState => _connectionState;
#pragma warning disable CA1721
    public RpcMethodResolver ServerMethodResolver => _serverMethodResolver;
#pragma warning restore CA1721
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
        // ServiceRegistry is resolved in a lazy fashion in RpcHub.
        // We access it here to make sure any configuration error gets thrown at this point.
        _ = hub.ServiceRegistry;

        Hub = hub;
        Ref = peerRef;
        ConnectionKind = hub.PeerConnectionKindResolver.Invoke(hub, peerRef);
        if (ConnectionKind is RpcPeerConnectionKind.None)
            ConnectionKind = RpcPeerConnectionKind.Remote; // RpcPeer.ConnectionKind should never be None
        Versions = versions ?? peerRef.Versions;
#pragma warning disable CA2214 // Do not call overridable methods in constructors
        // ReSharper disable once VirtualMemberCallInConstructor
        _serverMethodResolver = GetServerMethodResolver(null);
#pragma warning restore CA2214

        SerializationFormat = Hub.SerializationFormats.Get(peerRef);
        ArgumentSerializer = SerializationFormat.ArgumentSerializer;
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
            return sender is null || sender.TryWrite(message)
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
        => ConnectionState.Value.Handshake is not null;

    public bool IsConnected(
        [NotNullWhen(true)] out RpcHandshake? handshake,
        [NotNullWhen(true)] out ChannelWriter<RpcMessage>? sender)
    {
        var connectionState = ConnectionState.Value;
        handshake = connectionState.Handshake;
        sender = connectionState.Sender;
        return handshake is not null;
    }

    public async Task<(RpcHandshake Handshake, ChannelWriter<RpcMessage> Sender)> WhenConnected(
        CancellationToken cancellationToken = default)
    {
        var connectionState = ConnectionState;
        while (true) {
            try {
                connectionState = await connectionState.Last.WhenConnected(cancellationToken).ConfigureAwait(false);
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

    public Task Disconnect(CancellationToken cancellationToken = default)
        => Disconnect(null, null, cancellationToken);
    public Task Disconnect(Exception? error, CancellationToken cancellationToken = default)
        => Disconnect(error, null, cancellationToken);
    public Task Disconnect(
        Exception? error, AsyncState<RpcPeerConnectionState>? expectedState,
        CancellationToken cancellationToken = default)
    {
        AsyncState<RpcPeerConnectionState> connectionState;
        lock (Lock) {
            // We want to make sure ConnectionState doesn't change while this method runs
            // and no one else cancels ReaderTokenSource
            connectionState = _connectionState;
            if (expectedState is not null && !ReferenceEquals(expectedState, connectionState))
                return Task.CompletedTask;
            if (connectionState.Value.Sender is null || connectionState.IsFinal)
                return Task.CompletedTask;
        }
        connectionState.Value.Sender.TryComplete(error);
        // NOTE(AY): It isn't critical to cancel the ReaderTokenSource:
        // if we complete the Sender, the reader will inevitably fail as well.
        // TODO(AY): Find out why the next line makes RpcReconnectionTest.ConcurrentTest hang on disconnects.
        // connectionState.Value.ReaderTokenSource.CancelAndDisposeSilently();
        return connectionState.WhenDisconnected(cancellationToken);
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
            await TaskExt.NeverEnding(cancellationToken).ConfigureAwait(false);
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
                var readerTokenSource = cancellationToken.CreateLinkedTokenSource();
                var readerToken = readerTokenSource.Token;
                var isHandshakeError = false;
                try {
                    if (connectionState.IsFinal)
                        return;

                    if (connectionState.Value.IsConnected())
                        connectionState = SetConnectionState(connectionState.Value.NextDisconnected(), connectionState).RequireNonFinal();

                    // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                    var connection = await GetConnection(connectionState.Value, cancellationToken).ConfigureAwait(false);
                    var channel = connection.Channel;
                    var sender = channel.Writer;
                    var readAllUnbufferedChannel = channel as IChannelWithReadAllUnbuffered<RpcMessage>;
                    var reader = readAllUnbufferedChannel?.UseReadAllUnbuffered == true
                        ? readAllUnbufferedChannel.ReadAllUnbuffered(readerToken).GetAsyncEnumerator(readerToken)
                        : channel.Reader.ReadAllAsync(readerToken).GetAsyncEnumerator(readerToken);

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
                                    .WaitAsync(handshakeToken)
                                    .ConfigureAwait(false);
                                var hasMore = await reader.MoveNextAsync().ConfigureAwait(false);
                                if (!hasMore)
                                    throw new ChannelClosedException(); // Mimicking channel behavior here
                                var message = reader.Current;
                                var handshakeContext = ProcessMessage(message, handshakeToken, handshakeToken);
                                var remoteHandshake = handshakeContext?.Call.Arguments?.GetUntyped(0) as RpcHandshake;
                                return remoteHandshake ?? throw Errors.HandshakeFailed();
                            }, handshakeToken)
                            .WaitAsync(handshakeToken)
                            .ConfigureAwait(false);
                        if (handshake.RemoteApiVersionSet is null)
                            handshake = handshake with { RemoteApiVersionSet = new() };
                    }
                    catch (Exception e) {
                        isHandshakeError = true;
                        readerTokenSource.CancelAndDisposeSilently();
                        if (e.IsCancellationOf(handshakeToken) && !cancellationToken.IsCancellationRequested)
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

                    RpcInboundContext.Current = null;
                    Activity.Current = null;
                    try {
                        while (await reader.MoveNextAsync().ConfigureAwait(false))
                            _ = ProcessMessage(reader.Current, peerChangedToken, readerToken);
                    }
                    finally {
                        // Reset AsyncLocals that might be set by ProcessMessage
                        RpcInboundContext.Current = null;
                        Activity.Current = null;
                    }
                }
                catch (Exception e) {
                    var isReaderAbort = readerToken.IsCancellationRequested
                        && !cancellationToken.IsCancellationRequested
                        && !isHandshakeError;
                    error = isReaderAbort ? null : e;
                }
                finally {
                    readerTokenSource.CancelAndDisposeSilently();
                }

                if (cancellationToken.IsCancellationRequested) {
                    var isTerminal = error is not null && Hub.PeerTerminalErrorDetector.Invoke(error);
                    if (!isTerminal)
                        error = RpcReconnectFailedException.StopRequested(error);
                }
                connectionState = SetConnectionState(connectionState.Value.NextDisconnected(error));
                if (!connectionState.IsFinal)
                    continue;

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
                    var isTerminal = error is not null && Hub.PeerTerminalErrorDetector.Invoke(error);
                    if (!isTerminal)
                        error = new RpcReconnectFailedException(error);
                    SetConnectionState(connectionState.Value.NextDisconnected(error));
                }
                else if (error is null) {
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
            _ = context.Call.Process(cancellationToken);
            return context;
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            Log.LogError(e, "Failed to process inbound message: {Message}", message);
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
        if ((expectedState is not null && connectionState != expectedState) || ReferenceEquals(newState, oldState)) {
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
            if (newState.Error is not null && Hub.PeerTerminalErrorDetector.Invoke(newState.Error)) {
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
            if (terminalError is not null)
                Log.LogInformation("'{PeerRef}': Can't (re)connect, will shut down", Ref);
            else if (newState.IsConnected())
                Log.LogInformation("'{PeerRef}': Connected", Ref);
            else {
                var e = newState.Error;
                if (e is not null)
                    Log.LogInformation(e, "'{PeerRef}': Disconnected: {ErrorMessage}", Ref, e.Message);
                else
                    Log.LogInformation("'{PeerRef}': Disconnected", Ref);
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
