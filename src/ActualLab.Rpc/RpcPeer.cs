using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

public abstract class RpcPeer : WorkerBase, IHasId<Guid>
{
    private ILogger? _log;
    private RpcCallLogger? _callLogger;
    private AsyncState<RpcPeerConnectionState> _connectionState = new(RpcPeerConnectionState.Disconnected, true);
    private bool _resetTryIndex;
    private ChannelWriter<RpcMessage>? _sender;

    protected IServiceProvider Services => Hub.Services;
    protected internal ILogger Log => _log ??= Services.LogFor(GetType());
    protected internal RpcCallLogger CallLogger
        => _callLogger ??= Hub.CallLoggerFactory.Invoke(this, Hub.CallLoggerFilter, Log, CallLogLevel);
    protected internal ChannelWriter<RpcMessage>? Sender => _sender;

    public RpcHub Hub { get; }
    public RpcPeerRef Ref { get; }
    public VersionSet Versions { get; init; }
    public RpcServerMethodResolver ServerMethodResolver { get; protected set; }
    public int InboundConcurrencyLevel { get; init; } = 0; // 0 = no concurrency limit, 1 = one call at a time, etc.
    public RpcArgumentSerializer ArgumentSerializer { get; init; }
    public RpcInboundContextFactory InboundContextFactory { get; init; }
    public RpcInboundCallFilter InboundCallFilter { get; init; }
    public RpcInboundCallTracker InboundCalls { get; init; }
    public RpcOutboundCallTracker OutboundCalls { get; init; }
    public RpcRemoteObjectTracker RemoteObjects { get; init; }
    public RpcSharedObjectTracker SharedObjects { get; init; }
    public LogLevel CallLogLevel { get; init; } = LogLevel.None;
    public AsyncState<RpcPeerConnectionState> ConnectionState => _connectionState;
    public RpcPeerInternalServices InternalServices => new(this);
    public Guid Id { get; } = Guid.NewGuid();

    protected RpcPeer(RpcHub hub, RpcPeerRef @ref, VersionSet? versions)
    {
        // ServiceRegistry is resolved in lazy fashion in RpcHub.
        // We access it here to make sure any configuration error gets thrown at this point.
        _ = hub.ServiceRegistry;
        var services = hub.Services;
        Hub = hub;
        Ref = @ref;
        Versions = versions ?? @ref.GetVersions();
        ServerMethodResolver = Hub.ServiceRegistry.DefaultServerMethodResolver;

        ArgumentSerializer = Hub.ArgumentSerializer;
        InboundContextFactory = Hub.InboundContextFactory;
        InboundCallFilter = Hub.InboundCallFilter;
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
            if (sender == null || sender.TryWrite(message))
                return Task.CompletedTask;

            return CompleteSend(sender, message);
        }
        catch (Exception e) {
            Log.LogError(e, "Send failed");
            return Task.CompletedTask;
        }
    }

    public Task WhenConnected(TimeSpan timeout, CancellationToken cancellationToken)
    {
        return ConnectionState.Value.IsConnected()
            ? Task.CompletedTask
            : timeout > TimeSpan.Zero && timeout != TimeSpan.MaxValue
                ? WhenConnectedAsync(this, timeout, cancellationToken)
                : ConnectionState.WhenConnected(cancellationToken);

        static async Task WhenConnectedAsync(RpcPeer peer, TimeSpan timeout1, CancellationToken cancellationToken1)
        {
            using var timeoutCts = cancellationToken1.CreateLinkedTokenSource();
            var timeoutToken = timeoutCts.Token;
            timeoutCts.CancelAfter(timeout1);
            try {
                await peer.ConnectionState.WhenConnected(timeoutToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutToken.IsCancellationRequested) {
                if (cancellationToken1.IsCancellationRequested)
                    throw; // Not a timeout

                throw Errors.Disconnected(peer);
            }
        }
    }

    public Task Disconnect(
        bool abortReader = false,
        Exception? writeError = null,
        AsyncState<RpcPeerConnectionState>? expectedState = null)
    {
        AsyncState<RpcPeerConnectionState> connectionState;
        CancellationTokenSource? readerAbortSource;
        ChannelWriter<RpcMessage>? sender;
        lock (Lock) {
            // We want to make sure ConnectionState doesn't change while this method runs
            // and no one else cancels ReaderAbortSource
            connectionState = _connectionState;
            if (expectedState != null && expectedState != connectionState)
                return Task.CompletedTask;
            if (connectionState.IsFinal || !connectionState.Value.IsConnected())
                return Task.CompletedTask;

            sender = _sender;
            readerAbortSource = connectionState.Value.ReaderAbortSource;
            _sender = null;
        }
        sender?.TryComplete(writeError);
        if (abortReader)
            readerAbortSource.CancelAndDisposeSilently();
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
        var semaphore = InboundConcurrencyLevel > 1
            ? new SemaphoreSlim(InboundConcurrencyLevel, InboundConcurrencyLevel)
            : null;
        var connectionState = ConnectionState;
        var lastHandshake = (RpcHandshake?)null;
        var peerChangedSource = cancellationToken.CreateLinkedTokenSource();
        var peerChangedToken = peerChangedSource.Token;
        try {
            while (true) {
                var readerAbortToken = CancellationToken.None;
                var error = (Exception?)null;
                try {
                    if (connectionState.IsFinal)
                        return;

                    if (connectionState.Value.IsConnected())
                        connectionState = SetConnectionState(connectionState.Value.NextDisconnected(), connectionState);
                    var connection = await GetConnection(connectionState.Value, cancellationToken).ConfigureAwait(false);
                    if (connection == null)
                        throw Errors.ConnectionUnrecoverable();

                    var channel = connection.Channel;
                    var sender = channel.Writer;
                    var reader = channel.Reader;

                    // Sending Handshake call
                    var handshakeCts = new CancellationTokenSource();
                    var handshakeToken = handshakeCts.Token;
                    _ = Hub.Clock
                        .Delay(Hub.Limits.HandshakeTimeout, readerAbortToken)
                        .ContinueWith(_ => handshakeCts.CancelAndDisposeSilently(), TaskScheduler.Default);
                    RpcHandshake handshake;
                    try {
                        handshake = await Task
                            .Run(async () => {
                                await Hub.SystemCallSender
                                    .Handshake(this, sender, new RpcHandshake(Id, Versions))
                                    .ConfigureAwait(false);
                                var message = await reader.ReadAsync(handshakeToken).ConfigureAwait(false);
                                var handshakeContext = await ProcessMessage(message, handshakeToken).ConfigureAwait(false);
                                var handshake1 = (handshakeContext?.Call.Arguments as ArgumentList<RpcHandshake>)?.Item0;
                                return handshake1 ?? throw Errors.HandshakeFailed();
                            }, handshakeToken)
                            .WaitAsync(handshakeToken)
                            .ConfigureAwait(false);
                        if (handshake.RemoteApiVersionSet == null)
                            handshake = handshake with { RemoteApiVersionSet = new() };
                    }
                    catch (OperationCanceledException) {
                        if (!readerAbortToken.IsCancellationRequested && handshakeToken.IsCancellationRequested)
                            throw Errors.HandshakeTimeout();
                        throw;
                    }

                    // Processing Handshake
                    var isPeerChanged = lastHandshake != null && lastHandshake.RemotePeerId != handshake.RemotePeerId;
                    if (isPeerChanged) {
                        // Remote RpcPeer changed -> we must abort every call/object
                        peerChangedSource.CancelAndDisposeSilently();
                        peerChangedSource = cancellationToken.CreateLinkedTokenSource();
                        peerChangedToken = peerChangedSource.Token;
                        await Reset(Errors.PeerChanged()).ConfigureAwait(false);
                    }

                    var readerAbortSource = cancellationToken.CreateLinkedTokenSource();
                    readerAbortToken = readerAbortSource.Token;
                    connectionState = SetConnectionState(
                        connectionState.Value.NextConnected(connection, handshake, readerAbortSource),
                        connectionState);
                    if (connectionState.Value.Connection != connection)
                        continue;

                    // Recovery: re-send keep-alive object set & all outbound calls
                    var connectionStateValue = connectionState.Value;
                    _ = Task.Run(async () => {
                        _ = SharedObjects.Maintain(connectionStateValue, readerAbortToken);
                        _ = RemoteObjects.Maintain(connectionStateValue, readerAbortToken);
                        foreach (var outboundCall in OutboundCalls) {
                            readerAbortToken.ThrowIfCancellationRequested();
                            await outboundCall.Reconnect(isPeerChanged, readerAbortToken).ConfigureAwait(false);
                        }
                    }, readerAbortToken);

                    RpcMessage? message;
                    if (semaphore == null)
                        while (await reader.WaitToReadAsync(readerAbortToken).ConfigureAwait(false)) {
                            while (reader.TryRead(out message))
                                _ = ProcessMessage(message, peerChangedToken);
                        }
                    else
                        while (await reader.WaitToReadAsync(readerAbortToken).ConfigureAwait(false))
                        while (reader.TryRead(out message)) {
                            if (Equals(message.Service, RpcSystemCalls.Name.Value)) {
                                // System calls are exempt from semaphore use
                                _ = ProcessMessage(message, peerChangedToken);
                            }
                            else {
                                await semaphore.WaitAsync(readerAbortToken).ConfigureAwait(false);
                                _ = ProcessMessage(message, semaphore, peerChangedToken);
                            }
                        }
                }
                catch (Exception e) {
                    var isReaderAbort = readerAbortToken.IsCancellationRequested
                        && !cancellationToken.IsCancellationRequested;
                    error = isReaderAbort ? null : e;
                }
                // Inbound calls are auto-aborted via peerChangedToken from OnRun,
                // which becomes RpcInboundCallContext.CancellationToken.
                InboundCalls.Clear();
                connectionState = SetConnectionState(connectionState.Value.NextDisconnected(error));
                if (Ref.IsObsolete)
                    OutboundCalls.TryReroute();
            }
        }
        finally {
            peerChangedSource.CancelAndDisposeSilently();
        }
    }

    protected override Task OnStart(CancellationToken cancellationToken)
    {
        Log.LogInformation("'{PeerRef}': Started", Ref);
        foreach (var peerTracker in Hub.PeerTrackers)
            peerTracker.Invoke(this);
        return Task.CompletedTask;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
#pragma warning disable IL2046
    protected override Task OnStop()
#pragma warning restore IL2046
    {
        _ = DisposeAsync();
        Hub.Peers.TryRemove(Ref, this);

        // We want to make sure the sequence of ConnectionStates terminates for sure
        Exception error;
        Monitor.Enter(Lock);
        try {
            if (_connectionState.IsFinal)
                error = _connectionState.Value.Error
                    ?? ActualLab.Internal.Errors.InternalError("The exception wasn't provided on peer termination.");
            else {
                error = Errors.ConnectionUnrecoverable(_connectionState.Value.Error);
                SetConnectionState(_connectionState.Value.NextDisconnected(error));
            }
        }
        catch (Exception e) {
            // Not sure how we might land here, but we still need to report an error, so...
            error = e;
        }
        finally {
            Monitor.Exit(Lock);
        }
        return Reset(error, true);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    protected async Task Reset(Exception error, bool isStopped = false)
    {
        RemoteObjects.Abort();
        await SharedObjects.Abort(error).ConfigureAwait(false);
        if (isStopped)
            await OutboundCalls.Abort(error).ConfigureAwait(false);
        // Inbound calls are auto-aborted via peerChangedToken from OnRun,
        // which becomes RpcInboundCallContext.CancellationToken.
        // We clear them on Reset mostly "just in case": they're cleared
        // on every disconnect anyway.
        InboundCalls.Clear();
        Log.LogInformation("'{PeerRef}': {Action}", Ref, isStopped ? "Stopped" : "Peer changed");
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

    // Private methods

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
            _connectionState = connectionState = connectionState.SetNext(newState);
            try {
                ServerMethodResolver =
                    Hub.ServiceRegistry.GetServerMethodResolver(newState.Handshake?.RemoteApiVersionSet);
            }
            catch (Exception e) {
                Log.LogError(e, "[LegacyName] conflict");
                ServerMethodResolver = Hub.ServiceRegistry.DefaultServerMethodResolver;
            }
            if (newState.Error != null && Hub.UnrecoverableErrorDetector.Invoke(newState.Error, StopToken)) {
                terminalError = newState.Error is ConnectionUnrecoverableException
                    ? newState.Error
                    : Errors.ConnectionUnrecoverable(newState.Error);
                connectionState.TrySetFinal(terminalError);
                throw terminalError;
            }
            return connectionState;
        }
        finally {
            if (newState.ReaderAbortSource != oldState.ReaderAbortSource) {
                oldState.ReaderAbortSource.CancelAndDisposeSilently();
                _sender = newState.Channel?.Writer;
            }
            if (newState.Connection != oldState.Connection)
                oldState.Channel?.Writer.TryComplete(newState.Error); // Reliably shut down the old channel
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

    private async Task CompleteSend(ChannelWriter<RpcMessage> sender, RpcMessage message)
    {
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
