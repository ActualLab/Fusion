using System.Diagnostics;
using ActualLab.Generators;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc;

/// <summary>
/// Abstract base class representing one side of an RPC communication channel,
/// managing connection state, message serialization, and call tracking.
/// </summary>
public abstract class RpcPeer : WorkerBase, IHasId<Guid>
{
    public static LogLevel DefaultCallLogLevel { get; set; } = LogLevel.None;

    private volatile AsyncState<RpcPeerConnectionState> _connectionState = new(new RpcPeerConnectionState());
    private volatile RpcMethodResolver _serverMethodResolver;
    private volatile RpcTransport? _transport;
    private volatile RpcPeerStopMode _stopMode;
    private bool _resetConnectionAttemptIndex;

    protected internal readonly IServiceProvider Services;
    protected internal readonly RpcPeerOptions Options;
    protected internal readonly RpcInboundCallOptions InboundCallOptions;
    protected internal readonly RpcOutboundCallOptions OutboundCallOptions;

    protected internal RpcTransport? Transport
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            // Only expose Transport once the handshake has completed: outbound calls
            // (e.g. stream sends) read Peer.Transport directly, and writing to the new
            // channel before handshake messages have been exchanged corrupts the
            // remote peer's handshake (it reads our outbound message instead of our
            // handshake). _transport is set after _connectionState, so can lag — fall
            // through to ConnectionState only when it's actually connected.

            // Fast path: most sends happen while connected, so avoid reading
            // _connectionState unless _transport hasn't caught up yet.
            var transport = _transport;
            if (transport is not null)
                return transport;

            // _transport is set after _connectionState, so after a transition to
            // Connected there is a tiny window where the new transport is visible
            // only through ConnectionState. Handshaking states are intentionally
            // filtered out here: their Connection is exposed for teardown, not sends.
            var connectionState = _connectionState.Value;
            return connectionState.Handshake is not null ? connectionState.Transport : null;
        }
    }

    protected internal RpcCallLogger CallLogger
        => field ??= Hub.DiagnosticsOptions.CallLoggerFactory.Invoke(this, Log, CallLogLevel);
    protected internal ILogger Log
        => field ??= Services.LogFor(GetType());
    protected internal ILogger? DebugLog => Log.IfEnabled(LogLevel.Debug);

    public RpcHub Hub { get; }
    public RpcRoute Route { get; }
    public RpcRef Ref => Route.Ref;
    public MutablePropertyBag Extensions { get; init; }
    public Guid Id { get; } = Guid.NewGuid();
    public Moment CreatedAt { get; } = Moment.Now;
    public Moment LastKeepAliveAt => SharedObjects.LastKeepAliveAt;
    public RpcPeerConnectionKind ConnectionKind { get; }
    public VersionSet Versions { get; init; }
    public RpcSerializationFormat SerializationFormat { get; init; }
    public RpcArgumentSerializer ArgumentSerializer { get; init; }
    public RpcMessageSerializer MessageSerializer { get; init; }
    public Func<ReadOnlyMemory<byte>, string> Hasher { get; init; }
    public RpcInboundCallTracker InboundCalls { get; init; }
    public RpcOutboundCallTracker OutboundCalls { get; init; }
    public RpcRemoteObjectTracker RemoteObjects { get; init; }
    public RpcSharedObjectTracker SharedObjects { get; init; }
    public RpcPeerInternalServices InternalServices => new(this);
    public LogLevel CallLogLevel { get; init; } = DefaultCallLogLevel;

    public AsyncState<RpcPeerConnectionState> ConnectionState => _connectionState;
#pragma warning disable CA1721
    public RpcMethodResolver ServerMethodResolver {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _serverMethodResolver;
    }
#pragma warning restore CA1721

    public RpcPeerStopMode StopMode
    {
        get => _stopMode;
        set
        {
            lock (Lock)
                _stopMode = value;
        }
    }

    protected RpcPeer(RpcHub hub, RpcRoute route, VersionSet? versions)
    {
        // ServiceRegistry is resolved in a lazy fashion in RpcHub.
        // We access it here to make sure any configuration error gets thrown at this point.
        _ = hub.ServiceRegistry;

        Hub = hub;
        Services = hub.Services;
        Route = route;
        Options = Hub.PeerOptions;
        Extensions = new MutablePropertyBag();
        InboundCallOptions = Hub.InboundCallOptions;
        OutboundCallOptions = Hub.OutboundCallOptions;
        ConnectionKind = route.ConnectionKind;
        if (ConnectionKind is RpcPeerConnectionKind.None)
            ConnectionKind = Options.ConnectionKindDetector.Invoke(route);
        if (ConnectionKind is RpcPeerConnectionKind.None)
            ConnectionKind = RpcPeerConnectionKind.Remote; // RpcPeer.ConnectionKind should never be None
        Versions = versions ?? route.Ref.Versions;
#pragma warning disable CA2214 // Do not call overridable methods in constructors
        // ReSharper disable once VirtualMemberCallInConstructor
        _serverMethodResolver = GetServerMethodResolver(handshake: null);
#pragma warning restore CA2214

        SerializationFormat = Hub.SerializationFormats.Get(route.Ref.SerializationFormat);
        ArgumentSerializer = SerializationFormat.ArgumentSerializer;
        MessageSerializer = SerializationFormat.MessageSerializerFactory.Invoke(this);
        Hasher = OutboundCallOptions.Hasher;

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
        => $"{GetType().Name}({Route}, #{GetHashCode()})";

    // WhenConnected

    public Task<RpcPeerConnectionState> WhenConnected(CancellationToken cancellationToken = default)
        => ConnectionState.Value.WhenConnected.WaitAsync(cancellationToken);

    public Task<RpcPeerConnectionState> WhenConnected(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return timeout == TimeSpan.MaxValue
            ? WhenConnected(cancellationToken)
            : WhenConnectedWithTimeout(this, timeout, cancellationToken);

        static async Task<RpcPeerConnectionState> WhenConnectedWithTimeout(
            RpcPeer peer, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timeoutCts = cancellationToken.CreateLinkedTokenSource(timeout);
            var timeoutToken = timeoutCts.Token;
            try {
                return await peer.WhenConnected(timeoutToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e.IsCancellationOfTimeoutToken(timeoutToken, cancellationToken)) {
                throw Errors.ConnectTimeout(peer.Ref);
            }
        }
    }

    // WhenConnectedOrReroute

    public async Task<RpcPeerConnectionState> WhenConnectedOrReroute(CancellationToken cancellationToken = default)
    {
        Route.ThrowIfChanged();
        try {
            return await ConnectionState.Value.WhenConnected.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            Route.ThrowIfChanged();
            throw;
        }
    }

    public Task<RpcPeerConnectionState> WhenConnectedOrReroute(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return timeout == TimeSpan.MaxValue
            ? WhenConnectedOrReroute(cancellationToken)
            : WhenConnectedOrRerouteWithTimeout(this, timeout, cancellationToken);

        static async Task<RpcPeerConnectionState> WhenConnectedOrRerouteWithTimeout(
            RpcPeer peer, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timeoutCts = cancellationToken.CreateLinkedTokenSource(timeout);
            var timeoutToken = timeoutCts.Token;
            try {
                return await peer.WhenConnectedOrReroute(timeoutToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e.IsCancellationOfTimeoutToken(timeoutToken, cancellationToken)) {
                throw Errors.ConnectTimeout(peer.Ref);
            }
        }
    }

    // Disconnect

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
            if (connectionState.Value.Transport is null || connectionState.IsFinal)
                return Task.CompletedTask;
        }
        connectionState.Value.ReaderTokenSource.CancelAndDisposeSilently();
        // The line below isn't necessary: stopping the reader aborts everything
        // connectionState.Value.Sender.TryComplete(error);
        return connectionState.Value.WhenDisconnected.WaitAsync(cancellationToken);
    }

    // WhenDisconnected

    public Task WhenDisconnected(CancellationToken cancellationToken = default)
        => ConnectionState.Value.WhenDisconnected.WaitAsync(cancellationToken);

    // ResetConnectionAttemptIndex

    public void ResetConnectionAttemptIndex()
    {
        lock (Lock)
            _resetConnectionAttemptIndex = true;
    }

    // Protected methods

    protected abstract Task<RpcConnection> GetConnection(
        RpcPeerConnectionState connectionState,
        CancellationToken cancellationToken);

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        Log.LogInformation("'{Route}': Started ({ConnectionKind})", Route, ConnectionKind);

        // ReSharper disable once UseAwaitUsing
        // ReSharper disable once RedundantAssignment
        using var routeChangedTokenRegistration = Route.ChangedToken.Register(
            () => Task.Run(DisposeAsync, CancellationToken.None));

        var handshakeIndex = Options.UseRandomHandshakeIndex
            ? RandomShared.Next().PositiveModulo(65_537) // Prime
            : 0;
        var connectionState = ConnectionState;
        var lastHandshake = (RpcHandshake?)null;
        var peerChangedCts = cancellationToken.CreateLinkedTokenSource();
        var peerChangedToken = peerChangedCts.Token;
        try {
            if (ConnectionKind is RpcPeerConnectionKind.Local) {
                // It's a fake RpcPeer that exists solely to be "available"
                // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                await TaskExt.NeverEnding(cancellationToken).ConfigureAwait(false);
                return;
            }

            while (true) {
                var error = (Exception?)null;
                var connectedAt = Hub.SystemClock.Now;
                var readerTokenSource = cancellationToken.CreateLinkedTokenSource();
                var readerToken = readerTokenSource.Token;
                var isHandshakeError = false;
                Task maintainTask = Task.CompletedTask;
                try {
                    if (connectionState.IsFinal)
                        return;

                    if (connectionState.Value.Connection is not null)
                        connectionState = SetConnectionState(connectionState.Value.NextDisconnected(), connectionState)
                            .RequireNonFinal();

                    // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                    var connection =
                        await GetConnection(connectionState.Value, cancellationToken).ConfigureAwait(false);
                    var transport = connection.Transport;

                    // Expose the in-flight connection via _connectionState BEFORE the handshake
                    // exchange, so an external Disconnect() (e.g. RpcWebSocketServer.Invoke when a
                    // new connection arrives for the same rpcRef) can cancel the handshake reader
                    // and let this loop iterate to pick up the next queued connection. Without this,
                    // Disconnect short-circuits while Transport is null and incoming connections pile
                    // up against a peer stuck waiting for a handshake that never arrives.
                    // The Transport property still returns null until handshake completes, so
                    // outbound calls don't try to send through a channel before the handshake.
                    connectionState = SetConnectionState(
                        connectionState.Value.NextHandshaking(connection, readerTokenSource),
                        connectionState).RequireNonFinal();

                    // Get inbound message reader from the connection
                    var reader = connection.InboundMessages.GetAsyncEnumerator(readerToken);

                    // Sending Handshake call
                    using var handshakeCts = cancellationToken.CreateLinkedTokenSource(Hub.Limits.HandshakeTimeout);
                    var handshakeToken = handshakeCts.Token;
                    RpcHandshake handshake;
                    RpcHandshake ownHandshake;
                    try {
                        DebugLog?.LogDebug("'{Route}': Sending handshake", Route);
                        (handshake, ownHandshake) = await Task.Run(
                                async () => {
                                    var ownHandshake1 = new RpcHandshake(
                                        Id, Versions, Hub.Id,
                                        RpcHandshake.CurrentProtocolVersion,
                                        ++handshakeIndex);
                                    Hub.SystemCallSender.Handshake(this, transport, ownHandshake1);
                                    var hasMore = await reader.MoveNextAsync().ConfigureAwait(false);
                                    if (!hasMore)
                                        throw new ChannelClosedException(); // Mimicking channel behavior here

                                    var message = reader.Current;
                                    var remoteHandshake = ProcessHandshake(message, handshakeToken);
                                    return (remoteHandshake, ownHandshake1);
                                }, handshakeToken)
                            .WaitAsync(handshakeToken)
                            .ConfigureAwait(false);
                        if (handshake.ProtocolVersion is < RpcHandshake.MinimumProtocolVersion
                            or > RpcHandshake.CurrentProtocolVersion)
                            throw Errors.UnsupportedProtocolVersion(
                                handshake.ProtocolVersion,
                                RpcHandshake.MinimumProtocolVersion,
                                RpcHandshake.CurrentProtocolVersion);
                        if (handshake.RemoteApiVersionSet is null)
                            handshake = handshake with { RemoteApiVersionSet = new() };
                    }
                    catch (Exception e) {
                        Log.LogWarning(e, "'{Route}': Failed to send handshake", Route);
                        isHandshakeError = true;
                        readerTokenSource.CancelAndDisposeSilently();
                        if (e.IsCancellationOfTimeoutToken(handshakeToken, cancellationToken))
                            throw Errors.HandshakeTimeout();

                        throw;
                    }

                    // Processing Handshake
                    var peerChangeKind = handshake.GetPeerChangeKind(lastHandshake);
                    Log.LogInformation(
                        "'{Route}': Handshake succeeded, PeerChangeKind={PeerChangeKind}",
                        Route, peerChangeKind);
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
                    var nextConnectionState =
                        connectionState.Value.NextConnected(connection, handshake, ownHandshake, readerTokenSource);
                    connectionState = SetConnectionState(nextConnectionState, connectionState).RequireNonFinal();
                    var connectionStateValue = connectionState.Value;
                    if (connectionStateValue.Connection != connection)
                        continue; // Somehow disconnected

                    connectedAt = Hub.SystemClock.Now;
                    maintainTask = Task.Run(async () => {
                        var tasks = new List<Task> {
                            SharedObjects.Maintain(connectionStateValue, readerToken),
                            RemoteObjects.Maintain(connectionStateValue, readerToken),
                            OutboundCalls.Maintain(connectionStateValue, readerToken)
                        };
                        if (peerChangeKind != RpcPeerChangeKind.ChangedToVeryFirst) {
                            var isPeerChanged = peerChangeKind == RpcPeerChangeKind.Changed;
                            tasks.Add(OutboundCalls.Reconnect(connectionStateValue, isPeerChanged, readerToken));
                        }

                        await Task.WhenAll(tasks).SilentAwait(false);
                    }, readerToken);

                    DebugLog?.LogDebug("'{Route}': Processing messages", Route);
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
                    Log.LogInformation(e, "'{Route}': Read loop ended", Route);
                    var isReaderAbort = readerToken.IsCancellationRequested
                        && !cancellationToken.IsCancellationRequested
                        && !isHandshakeError;
                    error = isReaderAbort ? null : e;
                }
                finally {
                    readerTokenSource.CancelAndDisposeSilently();
                    await maintainTask.SilentAwait(false);
                }

                // If the connection closed gracefully but was too short-lived,
                // treat it as an error to bump ConnectionAttemptIndex and apply reconnect delay
                if (error is null && Hub.SystemClock.Now - connectedAt < Hub.Limits.PrematureDisconnectTimeout)
                    error = Errors.PrematureDisconnect();

                if (cancellationToken.IsCancellationRequested) {
                    var isTerminal = error is not null && Options.TerminalErrorDetector.Invoke(this, error);
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
            Log.LogInformation("'{Route}': Stopping", Route);

            // Make sure the sequence of ConnectionStates terminates
            Exception? error;
            lock (Lock) {
                connectionState = _connectionState;
                error = connectionState.Value.Error;
                if (!connectionState.IsFinal) {
                    var isTerminal = error is not null && Options.TerminalErrorDetector.Invoke(this, error);
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
            await Reset(error!, isStopped: true).SilentAwait(false);

            var removeDelay = Options.PeerRemoveDelayProvider.Invoke(this);
            if (removeDelay <= TimeSpan.Zero)
                Hub.RemovePeer(this);
            else
                _ = Task.Run(async () => {
                    await Task.Delay(removeDelay, CancellationToken.None).ConfigureAwait(false);
                    Hub.RemovePeer(this);
                }, CancellationToken.None);
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
        Log.LogInformation("'{Route}': {Action}", Route, isStopped ? "Stopped" : "Peer changed");
    }

    protected RpcInboundContext? ProcessMessage(
        RpcInboundMessage message,
        CancellationToken peerChangedToken,
        CancellationToken cancellationToken)
    {
        try {
            var context = InboundCallOptions.ContextFactory.Invoke(this, message, peerChangedToken);
            _ = context.Call.Process(cancellationToken);
            return context;
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            Log.LogError(e, "Failed to process inbound message: {Message}", message);
            return null;
        }
    }

    private RpcHandshake ProcessHandshake(RpcInboundMessage message, CancellationToken cancellationToken)
    {
        var methodDef = Hub.SystemCallSender.HandshakeMethodDef;
        if (message.MethodRef != methodDef.Ref
            || message.CallTypeId != methodDef.CallType.Id
            || message.RelatedId != 0)
            throw Errors.HandshakeFailed(
                $"expected {methodDef.Ref} call, but got: MethodRef = {message.MethodRef}, " +
                $"CallTypeId = {message.CallTypeId}, RelatedId = {message.RelatedId}.");

        var context = InboundCallOptions.ContextFactory.Invoke(this, message, cancellationToken);
        if (!ReferenceEquals(context.MethodDef, methodDef))
            throw Errors.HandshakeFailed($"the call is bound to an unexpected method: {context.MethodDef}.");

        _ = context.Call.Process(cancellationToken);
        return context.Call.Arguments?.GetUntyped(0) as RpcHandshake
            ?? throw Errors.HandshakeFailed("the call carries no RpcHandshake argument.");
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

    // Private methods

    // !!! This method can be called only from RpcPeer.OnRun!
    private AsyncState<RpcPeerConnectionState> SetConnectionState(
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
            if (newState.ConnectionAttemptIndex != 0 && _resetConnectionAttemptIndex) {
                newState.ConnectionAttemptIndex = 0;
                _resetConnectionAttemptIndex = false;
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
            if (newState.Error is not null && Options.TerminalErrorDetector.Invoke(this, newState.Error)) {
                terminalError = newState.Error;
                connectionState.TrySetFinal(terminalError);
            }
            return connectionState;
        }
        finally {
            // Only expose Transport once the handshake has completed: outbound calls (e.g. stream
            // sends) read Peer.Transport directly, and writing to the new channel before the
            // handshake messages have been exchanged corrupts the peer's handshake on the
            // remote side (the remote reads our outbound message instead of our handshake).
            _transport = newState.IsConnected() ? newState.Transport : null;
            // Order matters: fault first on terminal error so TrySetException wins over
            // any later TrySetResult on the same TCS. Covers both:
            //  - oldState's pending WhenDisconnected (if old was connected): fault, not success.
            //  - newState's pending WhenConnected (chained from old disconnected) + fresh
            //    _whenDisconnectedSource (we overrode the Always-shared one for terminal states).
            if (terminalError is not null) {
                oldState.MarkTerminated(terminalError);
                newState.MarkTerminated(terminalError);
            }
            else if (newState.IsConnected()) {
                oldState.MarkConnected(newState);
                // If the previous state was a transient handshaking state, also resolve its
                // _whenDisconnectedSource. An external Disconnect() that grabbed this state
                // races with the handshake completing here; without this, the waiter would
                // hang because MarkConnected only resolves _whenConnectedSource.
                if (oldState.IsConnecting())
                    oldState.MarkDisconnected();
            }
            else
                oldState.MarkDisconnected();

            if (newState.ReaderTokenSource != oldState.ReaderTokenSource)
                oldState.ReaderTokenSource.CancelAndDisposeSilently();
            if (newState.Connection != oldState.Connection)
                oldState.Transport?.TryComplete(newState.Error);
#if NET9_0_OR_GREATER
            Lock.Exit();
#else
            Monitor.Exit(Lock);
#endif
            if (this is RpcClientPeer clientPeer)
                Hub.Client.OnConnectionStateChange(clientPeer, connectionState.Value);

            // The code below is responsible solely for logging - all important stuff is already done
            if (terminalError is not null)
                Log.LogInformation("'{Route}': Can't (re)connect, will shut down", Route);
            else if (newState.IsConnected())
                Log.LogInformation("'{Route}': Connected", Route);
            else if (newState.IsConnecting())
                DebugLog?.LogDebug("'{Route}': Handshaking", Route);
            else {
                var e = newState.Error;
                if (e is not null)
                    Log.LogInformation(e, "'{Route}': Disconnected: {ErrorMessage}", Route, e.Message);
                else
                    Log.LogInformation("'{Route}': Disconnected", Route);
            }
        }
    }
}
