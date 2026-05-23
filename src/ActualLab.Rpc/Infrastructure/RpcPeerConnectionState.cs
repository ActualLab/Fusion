namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable CA1822 // Member can be made static

/// <summary>
/// Immutable snapshot of an RPC peer's connection state, including handshake, transport, and error info.
/// </summary>
public sealed record RpcPeerConnectionState
{
    public static readonly TaskCompletionSource<Unit> AlwaysDisconnectedSource
        = TaskCompletionSourceExt.New<Unit>().WithResult(default);

    private readonly TaskCompletionSource<RpcPeerConnectionState> _whenConnectedSource;
    private readonly TaskCompletionSource<Unit> _whenDisconnectedSource;

    public readonly RpcConnection? Connection;
    public readonly RpcTransport? Transport;
    public readonly RpcHandshake? Handshake;
    public readonly RpcHandshake? OwnHandshake;
    public readonly Exception? Error;
    public readonly CancellationTokenSource? ReaderTokenSource;
    public int ConnectionAttemptIndex { get; internal set; }
    public RpcPeerConnectionStateKind Kind { get; private set; }
    public Task<RpcPeerConnectionState> WhenConnected => _whenConnectedSource.Task;
    public Task WhenDisconnected => _whenDisconnectedSource.Task;

    public RpcPeerConnectionState(
        RpcConnection? connection = null,
        RpcHandshake? handshake = null,
        RpcHandshake? ownHandshake = null,
        Exception? error = null,
        int connectionAttemptIndex = 0,
        CancellationTokenSource? readerTokenSource = null,
        TaskCompletionSource<RpcPeerConnectionState>? whenConnectedSource = null)
    {
        Connection = connection;
        Transport = connection?.Transport;
        Handshake = handshake;
        OwnHandshake = ownHandshake;
        Error = error;
        ConnectionAttemptIndex = connectionAttemptIndex;
        ReaderTokenSource = readerTokenSource;
        if (handshake is not null) {
            Kind = RpcPeerConnectionStateKind.Connected;
            _whenConnectedSource = TaskCompletionSourceExt.New<RpcPeerConnectionState>().WithResult(this);
            _whenDisconnectedSource = TaskCompletionSourceExt.New<Unit>();
        }
        else {
            Kind = connection is not null
                ? RpcPeerConnectionStateKind.Connecting
                : RpcPeerConnectionStateKind.Disconnected;
            _whenConnectedSource = whenConnectedSource ?? TaskCompletionSourceExt.New<RpcPeerConnectionState>();
            // Use a real TCS for "handshaking" states (Connection set, Handshake null) so
            // an external Disconnect() can await actual teardown via WhenDisconnected.
            // For pure "no connection" states the shared AlwaysDisconnectedSource is fine.
            _whenDisconnectedSource = connection is null
                ? AlwaysDisconnectedSource
                : TaskCompletionSourceExt.New<Unit>();
        }
    }

    // IsXxx

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConnected()
        => Kind is RpcPeerConnectionStateKind.Connected;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConnected(
        [NotNullWhen(true)] out RpcHandshake? handshake,
        [NotNullWhen(true)] out RpcTransport? transport)
    {
        handshake = Handshake;
        transport = Transport;
        return handshake is not null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConnecting()
        => Kind is RpcPeerConnectionStateKind.Connecting;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConnectingOrConnected()
        => ((int)Kind & 3) != 0; // 3 = Connecting | Connected

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDisconnected()
        => ((int)Kind & 3) == 0; // 3 = Connecting | Connected

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTerminal()
        => Kind is RpcPeerConnectionStateKind.Terminal;

    // MarkXxx

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkConnected(RpcPeerConnectionState connectionState)
        => _whenConnectedSource.TrySetResult(connectionState);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkDisconnected()
        => _whenDisconnectedSource.TrySetResult(default);

    public void MarkTerminated(Exception error)
    {
        Kind = RpcPeerConnectionStateKind.Terminal;
        _whenConnectedSource.TrySetException(error);
        _whenDisconnectedSource.TrySetException(error);
    }

    // NextXxx

    public RpcPeerConnectionState NextHandshaking(
        RpcConnection connection,
        CancellationTokenSource readerTokenSource)
        // Pass through _whenConnectedSource so callers awaiting WhenConnected on the
        // previous (non-connected) state continue to be served by the same TCS,
        // which gets resolved later when the state advances to Connected.
        => new(connection, null, null, null, ConnectionAttemptIndex, readerTokenSource, _whenConnectedSource);

    public RpcPeerConnectionState NextConnected(
        RpcConnection connection,
        RpcHandshake handshake,
        RpcHandshake ownHandshake,
        CancellationTokenSource readerTokenSource)
        => new(connection, handshake, ownHandshake, null, 0, readerTokenSource);

    public RpcPeerConnectionState NextDisconnected(Exception? error = null)
    {
        if (Connection is { } connection)
            _ = connection.DisposeAsync();
        var whenConnectedSource = Handshake is null
            ? _whenConnectedSource
            : null;
        var next = error is null
            ? new RpcPeerConnectionState(whenConnectedSource: whenConnectedSource)
            : new RpcPeerConnectionState(null, null, null, error, ConnectionAttemptIndex + 1, null, whenConnectedSource);
        return next;
    }
}
