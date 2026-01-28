namespace ActualLab.Rpc.Infrastructure;

// Base class for RPC transports. Serialization happens synchronously inside
// Send (protected by SemaphoreSlim), while actual sending is async.
public abstract class RpcTransport(RpcPeer peer, CancellationTokenSource? stopTokenSource)
    : ProcessorBase(stopTokenSource), IAsyncEnumerable<RpcInboundMessage>
{
    public RpcPeer Peer { get; } = peer;
    public abstract Task WhenCompleted { get; }
    public abstract Task WhenClosed { get; }

    public abstract Task Send(
        RpcOutboundMessage message,
        RpcSendErrorHandler errorHandler,
        CancellationToken cancellationToken = default);
    public abstract bool TryComplete(Exception? error = null);

    public abstract IAsyncEnumerator<RpcInboundMessage> GetAsyncEnumerator(CancellationToken cancellationToken = default);
}
