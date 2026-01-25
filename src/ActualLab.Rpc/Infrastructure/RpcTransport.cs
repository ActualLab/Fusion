namespace ActualLab.Rpc.Infrastructure;

// Base class for RPC transports. Serialization happens synchronously inside
// Write (protected by SemaphoreSlim), while actual sending is async.
public abstract class RpcTransport(CancellationTokenSource? stopTokenSource)
    : ProcessorBase(stopTokenSource), IAsyncEnumerable<RpcInboundMessage>
{
    public abstract Task WhenCompleted { get; }
    public abstract Task WhenClosed { get; }

    public abstract Task Write(RpcOutboundMessage message, CancellationToken cancellationToken = default);
    public abstract bool TryComplete(Exception? error = null);

    public abstract IAsyncEnumerator<RpcInboundMessage> GetAsyncEnumerator(CancellationToken cancellationToken = default);
}
