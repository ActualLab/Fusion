namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Immutable snapshot of an RPC peer's connection state, including handshake, transport, and error info.
/// </summary>
public sealed record RpcPeerConnectionState(
    RpcConnection? Connection = null,
    RpcHandshake? Handshake = null,
    RpcHandshake? OwnHandshake = null,
    Exception? Error = null,
    int ConnectionAttemptIndex = 0,
    CancellationTokenSource? ReaderTokenSource = null)
{
    public static readonly RpcPeerConnectionState Disconnected = new();

    // Transport for writing messages (handles serialization)
    public RpcTransport? Transport = Connection?.Transport;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConnected()
        => Handshake is not null;

    // NextXxx

#pragma warning disable CA1822
    public RpcPeerConnectionState NextConnected(
#pragma warning restore CA1822
        RpcConnection connection,
        RpcHandshake handshake,
        RpcHandshake ownHandshake,
        CancellationTokenSource readerTokenSource)
        => new(connection, handshake, ownHandshake, null, 0, readerTokenSource);

    public RpcPeerConnectionState NextDisconnected(Exception? error = null)
    {
        if (Connection is { } connection)
            _ = connection.DisposeAsync();
        return error is null
            ? Disconnected
            : new RpcPeerConnectionState(null, null, null, error, ConnectionAttemptIndex + 1);
    }
}
