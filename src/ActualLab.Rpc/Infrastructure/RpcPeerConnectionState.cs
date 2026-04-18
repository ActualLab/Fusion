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
    public int ConnectionAttemptIndex;
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
        var isConnected = handshake is not null;
        if (isConnected) {
            _whenConnectedSource = TaskCompletionSourceExt.New<RpcPeerConnectionState>().WithResult(this);
            _whenDisconnectedSource = TaskCompletionSourceExt.New<Unit>();
        }
        else {
            _whenConnectedSource = whenConnectedSource ?? TaskCompletionSourceExt.New<RpcPeerConnectionState>();
            _whenDisconnectedSource = AlwaysDisconnectedSource;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConnected()
        => Handshake is not null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkConnected(RpcPeerConnectionState connectionState)
        => _whenConnectedSource.TrySetResult(connectionState);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkDisconnected()
        => _whenDisconnectedSource.TrySetResult(default);

    public void MarkTerminated(Exception error)
    {
        _whenConnectedSource.TrySetException(error);
        _whenDisconnectedSource.TrySetException(error);
    }

    // NextXxx

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
