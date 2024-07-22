namespace ActualLab.Rpc.Infrastructure;

public sealed record RpcPeerConnectionState(
    RpcConnection? Connection = null,
    RpcHandshake? Handshake = null,
    Exception? Error = null,
    int TryIndex = 0,
    CancellationTokenSource? ReaderTokenSource = null)
{
    public static readonly RpcPeerConnectionState Disconnected = new();

    public Channel<RpcMessage>? Channel = Connection?.Channel;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConnected()
        => Connection != null;

    // NextXxx

#pragma warning disable CA1822
    public RpcPeerConnectionState NextConnected(
#pragma warning restore CA1822
        RpcConnection connection,
        RpcHandshake handshake,
        CancellationTokenSource readerTokenSource)
        => new(connection, handshake, null, 0, readerTokenSource);

    public RpcPeerConnectionState NextDisconnected(Exception? error = null)
        => error == null ? Disconnected
            : new RpcPeerConnectionState(null, null, error, TryIndex + 1);
}
