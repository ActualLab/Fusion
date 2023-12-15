using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Rpc.Infrastructure;

public sealed record RpcPeerConnectionState(
    RpcConnection? Connection = null,
    RpcHandshake? Handshake = null,
    Exception? Error = null,
    int TryIndex = 0,
    CancellationTokenSource? ReaderAbortSource = null)
{
    public static readonly RpcPeerConnectionState Disconnected = new();

    public Channel<RpcMessage>? Channel = Connection?.Channel;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConnected()
        => Connection != null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_0
    public bool IsConnected(out RpcConnection? connection)
#else
    public bool IsConnected([NotNullWhen(true)] out RpcConnection? connection)
#endif
    {
        connection = Connection;
        return connection != null;
    }

#pragma warning disable CA1822
    public RpcPeerConnectionState NextConnected(
#pragma warning restore CA1822
        RpcConnection connection,
        RpcHandshake handshake,
        CancellationTokenSource readerAbortToken)
        => new(connection, handshake, null, 0, readerAbortToken);

    public RpcPeerConnectionState NextDisconnected(Exception? error = null)
        => error == null ? Disconnected
            : new RpcPeerConnectionState(null, null, error, TryIndex + 1);
}
