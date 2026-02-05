using System.Diagnostics;
using ActualLab.Generators;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc;

public abstract class RpcPeer : WorkerBase, IHasId<Guid>
{
    public static LogLevel DefaultCallLogLevel { get; set; } = LogLevel.None;

    private volatile AsyncState<RpcPeerConnectionState> _connectionState = new(RpcPeerConnectionState.Disconnected);
    private volatile RpcMethodResolver _serverMethodResolver;
    private volatile RpcTransport? _transport;
    private volatile RpcPeerStopMode _stopMode;
    private bool _resetTryIndex;

    protected internal readonly IServiceProvider Services;
    protected internal readonly RpcPeerOptions Options;
    protected internal readonly RpcInboundCallOptions InboundCallOptions;
    protected internal readonly RpcOutboundCallOptions OutboundCallOptions;

    protected internal RpcTransport? Transport
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _transport ?? _connectionState.Value.Transport; // _transport is set after _connectionState, so can be out of sync
    }

    protected internal RpcCallLogger CallLogger
        => field ??= Hub.DiagnosticsOptions.CallLoggerFactory.Invoke(this, Log, CallLogLevel);
    protected internal ILogger Log
        => field ??= Services.LogFor(GetType());
    protected internal ILogger? DebugLog => Log.IfEnabled(LogLevel.Debug);

    public RpcHub Hub { get; }
    public RpcPeerRef Ref { get; }
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
    public RpcMethodResolver ServerMethodResolver => _serverMethodResolver;
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

    protected RpcPeer(RpcHub hub, RpcPeerRef peerRef, VersionSet? versions)
    {
        // ServiceRegistry is resolved in a lazy fashion in RpcHub.
        // We access it here to make sure any configuration error gets thrown at this point.
        _ = hub.ServiceRegistry;

        Hub = hub;
        Services = hub.Services;
        Ref = peerRef;
        Options = Hub.PeerOptions;
        InboundCallOptions = Hub.InboundCallOptions;
        OutboundCallOptions = Hub.OutboundCallOptions;
        ConnectionKind = Options.ConnectionKindDetector.Invoke(peerRef);
        if (ConnectionKind is RpcPeerConnectionKind.None)
            ConnectionKind = RpcPeerConnectionKind.Remote; // RpcPeer.ConnectionKind should never be None
        Versions = versions ?? peerRef.Versions;
#pragma warning disable CA2214 // Do not call overridable methods in constructors
        // ReSharper disable once VirtualMemberCallInConstructor
        _serverMethodResolver = GetServerMethodResolver(handshake: null);
#pragma warning restore CA2214

        SerializationFormat = Hub.SerializationFormats.Get(peerRef.SerializationFormat);
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
        => $"{GetType().Name}({Ref}, #{GetHashCode()})";

    // Is/WhenConnected

    public bool IsConnected()
        => ConnectionState.Value.Handshake is not null;

    public bool IsConnected(
        [NotNullWhen(true)] out RpcHandshake? handshake,
        [NotNullWhen(true)] out RpcTransport? transport)
    {
        var connectionState = ConnectionState.Value;
        handshake = connectionState.Handshake;
        transport = connectionState.Transport;
        return handshake is not null;
    }

    public async Task<(RpcHandshake Handshake, RpcTransport Transport)> WhenConnected(
        CancellationToken cancellationToken = default)
    {
        var connectionState = ConnectionState;
        while (true) {
            try {
                connectionState = await connectionState.Last.WhenConnected(cancellationToken).ConfigureAwait(false);
                var vConnectionState = connectionState.Value;
                if (vConnectionState.Handshake is not { } handshake)
                    continue;
                if (vConnectionState.Transport is not { } transport)
                    continue;

                var spinWait = new SpinWait();
                while (!connectionState.HasNext) {
                    // Waiting for _transport assignment
                    if (ReferenceEquals(transport, _transport))
                        return (handshake, transport);

                    spinWait.SpinOnce();
                }
            }
            catch (Exception e) {
                if (e.IsCancellationOf(cancellationToken))
                    throw;
                if (Ref.RouteState.IsChanged())
                    throw RpcRerouteException.MustReroute();

                if (!ConnectionState.IsFinal) {
                    // The error can be ChannelClosedException due to cancellation,
                    // so we don't want to disregard the cancellation no matter what
                    cancellationToken.ThrowIfCancellationRequested();
                    continue;
                }

                throw;
            }
        }
    }

    public Task<(RpcHandshake Handshake, RpcTransport Transport)> WhenConnected(
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return timeout == TimeSpan.MaxValue
            ? WhenConnected(cancellationToken)
            : WhenConnectedWithTimeout(this, timeout, cancellationToken);

        static async Task<(RpcHandshake Handshake, RpcTransport Transport)> WhenConnectedWithTimeout(
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
        Log.LogInformation("'{PeerRef}': Started ({ConnectionKind})", Ref, ConnectionKind);

        // ReSharper disable once UseAwaitUsing
        // ReSharper disable once RedundantAssignment
        using var routeChangedTokenRegistration = Ref.RouteState?.ChangedToken.Register(
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
                var readerTokenSource = cancellationToken.CreateLinkedTokenSource();
                var readerToken = readerTokenSource.Token;
                var isHandshakeError = false;
                try {
                    if (connectionState.IsFinal)
                        return;

                    if (connectionState.Value.Connection is not null)
                        connectionState = SetConnectionState(connectionState.Value.NextDisconnected(), connectionState).RequireNonFinal();

                    // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                    var connection = await GetConnection(connectionState.Value, cancellationToken).ConfigureAwait(false);
                    var transport = connection.Transport;

                    // Get inbound message reader from the connection
                    var reader = connection.InboundMessages.GetAsyncEnumerator(readerToken);

                    // Sending Handshake call
                    using var handshakeCts = cancellationToken.CreateLinkedTokenSource(Hub.Limits.HandshakeTimeout);
                    var handshakeToken = handshakeCts.Token;
                    RpcHandshake handshake;
                    RpcHandshake ownHandshake;
                    try {
                        DebugLog?.LogDebug("'{PeerRef}': Sending handshake", Ref);
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
                                var handshakeContext = ProcessMessage(message, handshakeToken, handshakeToken);
                                var remoteHandshake = handshakeContext?.Call.Arguments?.GetUntyped(0) as RpcHandshake;
                                return (remoteHandshake ?? throw Errors.HandshakeFailed(), ownHandshake1);
                            }, handshakeToken)
                            .WaitAsync(handshakeToken)
                            .ConfigureAwait(false);
                        if (handshake.RemoteApiVersionSet is null)
                            handshake = handshake with { RemoteApiVersionSet = new() };
                    }
                    catch (Exception e) {
                        Log.LogWarning(e, "'{PeerRef}': Failed to send handshake", Ref);
                        isHandshakeError = true;
                        readerTokenSource.CancelAndDisposeSilently();
                        if (e.IsCancellationOfTimeoutToken(handshakeToken, cancellationToken))
                            throw Errors.HandshakeTimeout();
                        throw;
                    }

                    // Processing Handshake
                    var peerChangeKind = handshake.GetPeerChangeKind(lastHandshake);
                    Log.LogInformation(
                        "'{PeerRef}': Handshake succeeded, PeerChangeKind={PeerChangeKind}",
                        Ref, peerChangeKind);
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
                    var nextConnectionState = connectionState.Value.NextConnected(connection, handshake, ownHandshake, readerTokenSource);
                    connectionState = SetConnectionState(nextConnectionState, connectionState).RequireNonFinal();
                    var connectionStateValue = connectionState.Value;
                    if (connectionStateValue.Connection != connection)
                        continue; // Somehow disconnected

                    var maintainTasks = Task.Run(async () => {
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

                    DebugLog?.LogDebug("'{PeerRef}': Processing messages", Ref);
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
                        await maintainTasks.SilentAwait(false);
                    }
                }
                catch (Exception e) {
                    Log.LogInformation(e, "'{PeerRef}': Read loop ended", Ref);
                    var isReaderAbort = readerToken.IsCancellationRequested
                        && !cancellationToken.IsCancellationRequested
                        && !isHandshakeError;
                    error = isReaderAbort ? null : e;
                }

                readerTokenSource.CancelAndDisposeSilently();
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
            Log.LogInformation("'{PeerRef}': Stopping", Ref);

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
        Log.LogInformation("'{PeerRef}': {Action}", Ref, isStopped ? "Stopped" : "Peer changed");
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
            if (newState.Error is not null && Options.TerminalErrorDetector.Invoke(this, newState.Error)) {
                terminalError = newState.Error;
                connectionState.TrySetFinal(terminalError);
            }
            return connectionState;
        }
        finally {
            if (newState.ReaderTokenSource != oldState.ReaderTokenSource) {
                // Cancel the old ReaderTokenSource
                oldState.ReaderTokenSource.CancelAndDisposeSilently();
                _transport = newState.Transport;
            }
            if (newState.Connection != oldState.Connection) {
                // Complete the old transport
                oldState.Transport?.TryComplete(newState.Error);
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
