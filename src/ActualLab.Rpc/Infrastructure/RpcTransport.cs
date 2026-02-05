namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Base class for RPC transports that handle message serialization and sending.
/// </summary>
public abstract class RpcTransport(RpcPeer peer, CancellationTokenSource? stopTokenSource)
    : ProcessorBase(stopTokenSource), IAsyncEnumerable<RpcInboundMessage>
{
    public RpcPeer Peer { get; } = peer;
    public abstract Task WhenCompleted { get; }

    // This method should never throw - errors are reported via message.SendHandler
    public abstract void Send(RpcOutboundMessage message, CancellationToken cancellationToken = default);
    public abstract bool TryComplete(Exception? error = null);

    public abstract IAsyncEnumerator<RpcInboundMessage> GetAsyncEnumerator(CancellationToken cancellationToken = default);

    // Protected methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void CompleteSend(RpcOutboundMessage message)
        => message.SendHandler?.Invoke(this, message, error: null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void CompleteSend(RpcOutboundMessage message, Exception error)
        => (message.SendHandler ?? RpcSendHandlers.Default).Invoke(this, message, error);
}
