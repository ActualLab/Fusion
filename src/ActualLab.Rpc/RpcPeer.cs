using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc;

public abstract class RpcPeer : WorkerBase, IHasId<Guid>
{
    private static readonly HashSet<long> EmptyCallIdSet = new();
    public static LogLevel DefaultCallLogLevel { get; set; } = LogLevel.None;

    private AsyncState<RpcPeerConnectionState> _connectionState = new(RpcPeerConnectionState.Disconnected, true);
    private ChannelWriter<RpcMessage>? _sender;
    private bool _resetTryIndex;
    private volatile RpcPeerStopMode _stopMode;
    private volatile Action<byte[]>? _resumeHandler;
    private RpcCallLogger? _callLogger;
    private ILogger? _log;

    protected IServiceProvider Services => Hub.Services;
    protected internal ChannelWriter<RpcMessage>? Sender => _sender;

    protected internal RpcCallLogger CallLogger
        => _callLogger ??= Hub.CallLoggerFactory.Invoke(this, Hub.CallLoggerFilter, Log, CallLogLevel);
    protected internal ILogger Log
        => _log ??= Services.LogFor(GetType());

    public RpcHub Hub { get; }
    public RpcPeerRef Ref { get; }
    public Guid Id { get; } = Guid.NewGuid();
    public CpuTimestamp CreatedAt { get; } = CpuTimestamp.Now;
    public RpcPeerConnectionKind ConnectionKind { get; }
    public VersionSet Versions { get; init; }
    public RpcServerMethodResolver ServerMethodResolver { get; protected set; }
    public int InboundCallConcurrencyLevel { get; init; } = 0; // 0 = no concurrency limit, 1 = one call at a time, etc.
    public TimeSpan InboundCallCancellationOnStopDelay { get; init; }
    public RpcArgumentSerializer ArgumentSerializer { get; init; }
    public RpcHashProvider HashProvider { get; init; }
    public RpcInboundContextFactory InboundContextFactory { get; init; }
    public RpcInboundCallFilter InboundCallFilter { get; init; }
    public RpcInboundCallTracker InboundCalls { get; init; }
    public RpcOutboundCallTracker OutboundCalls { get; init; }
    public RpcRemoteObjectTracker RemoteObjects { get; init; }
    public RpcSharedObjectTracker SharedObjects { get; init; }
    public LogLevel CallLogLevel { get; init; } = DefaultCallLogLevel;
    public AsyncState<RpcPeerConnectionState> ConnectionState => _connectionState;
    public RpcPeerInternalServices InternalServices => new(this);

    public RpcPeerStopMode StopMode {
        get => _stopMode;
        set {
            lock (Lock)
                _stopMode = value;
        }
    }

    protected RpcPeer(RpcHub hub, RpcPeerRef @ref, VersionSet? versions)
    {
        // ServiceRegistry is resolved in lazy fashion in RpcHub.
        // We access it here to make sure any configuration error gets thrown at this point.
        _ = hub.ServiceRegistry;

        Hub = hub;
        Ref = @ref;
        ConnectionKind = @ref.GetConnectionKind(hub);
        Versions = versions ?? @ref.GetVersions();
        FlowExecutionContext = false; // Important: otherwise peers may "inherit" Activity from RpcHub.GetPeer

        ServerMethodResolver = Hub.ServiceRegistry.DefaultServerMethodResolver;
        ArgumentSerializer = Hub.ArgumentSerializer;
        HashProvider = Hub.HashProvider;
        InboundContextFactory = Hub.InboundContextFactory;
        InboundCallFilter = Hub.InboundCallFilter;
        InboundCallCancellationOnStopDelay = Ref.IsServer ? TimeSpan.FromSeconds(1) : TimeSpan.Zero;

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

    public Task Send(RpcMessage message, ChannelWriter<RpcMessage>? sender = null)
    {
        // !!! Send should never throw an exception.
        // This method is optimized to run as quickly as possible,
        // that's why it is a bit complicated.

        sender ??= Sender;
        try {
            return sender == null || sender.TryWrite(message)
                ? Task.CompletedTask
                : CompleteAsync();
        }
        catch (Exception e) {
            Log.LogError(e, "Send failed");
            return Task.CompletedTask;
        }

        async Task CompleteAsync() {
            // !!! This method should never fail
            try {
                // If we're here, WaitToWriteAsync call is required to continue
                while (await sender.WaitToWriteAsync(StopToken).ConfigureAwait(false))
                    if (sender.TryWrite(message))
                        return;

                throw new ChannelClosedException();
            }
#pragma warning disable RCS1075
            catch (Exception e) when (!e.IsCancellationOf(StopToken)) {
                Log.LogError(e, "Send failed");
            }
#pragma warning restore RCS1075
        }
    }

    public bool IsConnected()
        => ConnectionState.Value.Handshake != null;

    public bool IsConnected([NotNullWhen(true)] out RpcHandshake? handshake)
    {
        var connectionState = ConnectionState.Value;
        handshake = connectionState.Handshake;
        return handshake != null;
    }

    public async Task<RpcHandshake> WhenConnected(CancellationToken cancellationToken = default)
    {
        var connectionState = ConnectionState;
        while (true) {
            try {
                connectionState = await connectionState.WhenConnected(cancellationToken).ConfigureAwait(false);
                if (connectionState.Value.Handshake is { } handshake && !connectionState.HasNext)
                    return handshake;
            }
            catch (RpcReconnectFailedException) {
                if (Ref.IsRerouted)
                    throw RpcRerouteException.MustReroute();
                throw;
            }
        }
    }

    public Task<RpcHandshake> WhenConnected(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return timeout == TimeSpan.MaxValue
            ? WhenConnected(cancellationToken)
            : WhenConnectedWithTimeout(this, timeout, cancellationToken);

        static async Task<RpcHandshake> WhenConnectedWithTimeout(RpcPeer peer, TimeSpan timeout1, CancellationToken cancellationToken1) {
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
        Exception? writeError = null,
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
        sender?.TryComplete(writeError);
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

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
#pragma warning disable IL2046
    protected override async Task OnRun(CancellationToken cancellationToken)
#pragma warning restore IL2046
    {
        if (ConnectionKind == RpcPeerConnectionKind.Local) {
            // It's a fake RpcPeer that exists solely to be "available"
            await TaskExt.NewNeverEndingUnreferenced().WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        Log.LogInformation("'{PeerRef}': Started", Ref);
        foreach (var peerTracker in Hub.PeerTrackers)
            peerTracker.Invoke(this);

        var semaphore = InboundCallConcurrencyLevel > 0
            ? new SemaphoreSlim(InboundCallConcurrencyLevel, InboundCallConcurrencyLevel)
            : null;
        var connectionState = ConnectionState;
        var lastHandshake = (RpcHandshake?)null;
        var inboundCallTokenSource = cancellationToken.CreateDelayedTokenSource(InboundCallCancellationOnStopDelay);
        var inboundCallToken = inboundCallTokenSource.Token;
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
                                var ownHandshake = new RpcHandshake(Id, Versions, Hub.Id, RpcHandshake.CurrentProtocolVersion);
                                await Hub.SystemCallSender
                                    .Handshake(this, sender, ownHandshake)
                                    .ConfigureAwait(false);
                                var message = await reader.ReadAsync(handshakeToken).ConfigureAwait(false);
                                var handshakeContext = await ProcessMessage(message, handshakeToken).ConfigureAwait(false);
                                var remoteHandshake = (handshakeContext?.Call.Arguments as ArgumentList<RpcHandshake>)?.Item0;
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
                    var isRemotePeerChanged = lastHandshake != null && lastHandshake.RemotePeerId != handshake.RemotePeerId;
                    if (isRemotePeerChanged) {
                        // Remote RpcPeer changed -> we must abort every inbound call / shared object
                        inboundCallTokenSource.CancelAndDisposeSilently();
                        inboundCallTokenSource = cancellationToken.CreateLinkedTokenSource();
                        inboundCallToken = inboundCallTokenSource.Token;
                        await Reset(Errors.PeerChanged()).ConfigureAwait(false);
                    }

                    // Only at this point: expose the new connection state
                    var readerTokenSource = cancellationToken.CreateLinkedTokenSource();
                    readerToken = readerTokenSource.Token;
                    var nextConnectionState = connectionState.Value.NextConnected(connection, handshake, readerTokenSource);
                    connectionState = SetConnectionState(nextConnectionState, connectionState).RequireNonFinal();
                    if (connectionState.Value.Connection != connection)
                        continue; // Somehow disconnected

                    // Recovery: re-send keep-alive object set & all outbound calls
                    _ = Task.Run(() => Recover(handshake, isRemotePeerChanged, readerToken), readerToken);

                    RpcMessage? message;
                    if (semaphore == null)
                        while (await reader.WaitToReadAsync(readerToken).ConfigureAwait(false)) {
                            while (reader.TryRead(out message))
                                _ = ProcessMessage(message, inboundCallToken);
                        }
                    else
                        while (await reader.WaitToReadAsync(readerToken).ConfigureAwait(false))
                        while (reader.TryRead(out message)) {
                            if (Equals(message.Service, RpcSystemCalls.Name.Value)) {
                                // System calls are exempt from semaphore use
                                _ = ProcessMessage(message, inboundCallToken);
                            }
                            else {
                                await semaphore.WaitAsync(readerToken).ConfigureAwait(false);
                                _ = ProcessMessage(message, semaphore, inboundCallToken);
                            }
                        }
                }
                catch (Exception e) {
                    var isReaderAbort = readerToken.IsCancellationRequested
                        && !cancellationToken.IsCancellationRequested;
                    error = isReaderAbort ? null : e;
                }

                // Inbound calls are auto-aborted via inboundCallToken from OnRun,
                // which becomes RpcInboundCallContext.CancellationToken.
                InboundCalls.Clear();
                if (Ref.IsRerouted)
                    error = RpcRerouteException.MustReroute();
                else if (cancellationToken.IsCancellationRequested) {
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
            inboundCallTokenSource.CancelAndDisposeSilently(); // Terminates all running ProcessMessage calls
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
                    error = Ref.IsRerouted
                        ? RpcRerouteException.MustReroute()
                        : RpcReconnectFailedException.StopRequested();
                }
            }
            await Reset(error!, true).ConfigureAwait(false);
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    protected async Task Reset(Exception error, bool isStopped = false)
    {
        RemoteObjects.Abort();
        await SharedObjects.Abort(error).ConfigureAwait(false);
        if (isStopped)
            await OutboundCalls.Abort(error).ConfigureAwait(false);
        // Inbound calls are auto-aborted via inboundCallToken from OnRun,
        // which becomes RpcInboundCallContext.CancellationToken.
        // We clear them on Reset mostly "just in case": they're cleared
        // on every disconnect anyway.
        InboundCalls.Clear();
        Log.LogInformation("'{PeerRef}': {Action}", Ref, isStopped ? "Stopped" : "Peer changed");
    }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    protected async Task Recover(RpcHandshake handshake, bool isRemotePeerChanged, CancellationToken cancellationToken)
    {
        _ = SharedObjects.Maintain(handshake, cancellationToken);
        _ = RemoteObjects.Maintain(handshake, cancellationToken);
        if (handshake.ProtocolVersion < 1) {
            // Old peer w/o ISystemCalls.Resume method
            await ReconnectOutboundCalls(EmptyCallIdSet).ConfigureAwait(false);
            _ = OutboundCalls.Maintain(handshake, cancellationToken);
            return;
        }

        ReplaceResumeHandler(remoteInboundCallData => _ = Task.Run(async () => {
            var remoteInboundCallIds = IncreasingSeqPacker.Deserialize(remoteInboundCallData).ToHashSet();
            await ReconnectOutboundCalls(remoteInboundCallIds).ConfigureAwait(false);
            _ = OutboundCalls.Maintain(handshake, cancellationToken);
        }, CancellationToken.None));
        _ = Hub.SystemCallSender.Resume(this, InboundCalls.GetData())
            .ConfigureAwait(false);

        async Task ReconnectOutboundCalls(HashSet<long> remoteInboundCallIds) {
            foreach (var outboundCall in OutboundCalls) {
                cancellationToken.ThrowIfCancellationRequested();
                var isKnownToRemotePeer = remoteInboundCallIds.Contains(outboundCall.Id);
                await outboundCall
                    .Reconnect(isRemotePeerChanged, isKnownToRemotePeer, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    protected internal Action<byte[]>? ReplaceResumeHandler(Action<byte[]>? newHandler) {
        lock (Lock) {
            var oldHandler = _resumeHandler;
            _resumeHandler = newHandler;
            return oldHandler;
        }
    }

    protected async ValueTask<RpcInboundContext?> ProcessMessage(
        RpcMessage message,
        CancellationToken cancellationToken)
    {
        try {
            var context = InboundContextFactory.Invoke(this, message, cancellationToken);
            using var scope = context.Activate();
            await context.Call.Run().ConfigureAwait(false);
            return context;
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            Log.LogError(e, "Failed to process message: {Message}", message);
            return null;
        }
    }

    protected async ValueTask<RpcInboundContext?> ProcessMessage(
        RpcMessage message,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        try {
            var context = InboundContextFactory.Invoke(this, message, cancellationToken);
            using var scope = context.Activate();
            await context.Call.Run().ConfigureAwait(false);
            return context;
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            Log.LogError(e, "Failed to process message: {Message}", message);
            return null;
        }
        finally {
            semaphore.Release();
        }
    }

    protected AsyncState<RpcPeerConnectionState> SetConnectionState(
        RpcPeerConnectionState newState,
        AsyncState<RpcPeerConnectionState>? expectedState = null)
    {
        Monitor.Enter(Lock);
        var connectionState = _connectionState;
        var oldState = connectionState.Value;
        if ((expectedState != null && connectionState != expectedState) || ReferenceEquals(newState, oldState)) {
            Monitor.Exit(Lock);
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
                Monitor.Exit(Lock);
                return connectionState;
            }
            _connectionState = connectionState = nextConnectionState;
            try {
                ServerMethodResolver =
                    Hub.ServiceRegistry.GetServerMethodResolver(newState.Handshake?.RemoteApiVersionSet);
            }
            catch (Exception e) {
                Log.LogError(e, "[LegacyName] conflict");
                ServerMethodResolver = Hub.ServiceRegistry.DefaultServerMethodResolver;
            }
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
                _sender = newState.Channel?.Writer;
            }
            if (newState.Connection != oldState.Connection) {
                // Complete the old Channel
                oldState.Channel?.Writer.TryComplete(newState.Error);
            }
            Monitor.Exit(Lock);

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

    protected internal virtual RpcPeerStopMode ComputeAutoStopMode()
        => Ref.IsServer
            ? RpcPeerStopMode.KeepInboundCallsIncomplete // The client will likely reconnect or pick another server
            : RpcPeerStopMode.CancelInboundCalls; // When the client dies, server-to-client calls must be cancelled
}
