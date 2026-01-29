namespace ActualLab.Rpc.Infrastructure;

// Base class for RPC transports. Serialization happens synchronously inside
// Send (protected by SemaphoreSlim), while actual sending is async.
public abstract class RpcTransport : ProcessorBase, IAsyncEnumerable<RpcInboundMessage>
{
    protected readonly RpcSendErrorHandler DefaultSendErrorHandlerFunc;

    public RpcPeer Peer { get; }
    public abstract Task WhenCompleted { get; }
    public abstract Task WhenClosed { get; }

    protected RpcTransport(RpcPeer peer, CancellationTokenSource? stopTokenSource)
        : base(stopTokenSource)
    {
        Peer = peer;
        DefaultSendErrorHandlerFunc = (e, _, _) => DefaultSendErrorHandler(e);
    }

    // This variant of Send should never throw
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract Task Send(
        RpcOutboundMessage message,
        CancellationToken cancellationToken = default);

    // This variant of Send should never throw, but call errorHandler on error
    public abstract Task Send(
        RpcOutboundMessage message,
        RpcSendErrorHandler? errorHandler,
        CancellationToken cancellationToken = default);
    public abstract bool TryComplete(Exception? error = null);

    public abstract IAsyncEnumerator<RpcInboundMessage> GetAsyncEnumerator(CancellationToken cancellationToken = default);

    // Private methods

    protected void DefaultSendErrorHandler(Exception error)
        => Peer.Log.LogError(error, "Send failed");
}
