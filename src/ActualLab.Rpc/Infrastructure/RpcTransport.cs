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

    // CompleteSend must never throw: it runs on transport write loops, where
    // an escaping exception kills the writer and lets its pooled frame buffers
    // be disposed and reused while a frame flush may still be in flight
    protected void CompleteSend(RpcOutboundMessage message)
    {
        try {
            message.SendHandler?.Invoke(this, message, error: null);
        }
        catch (Exception e) {
            Peer.Log.LogError(e, "Send handler failed");
        }
    }

    protected void CompleteSend(RpcOutboundMessage message, Exception error)
    {
        try {
            (message.SendHandler ?? RpcSendHandlers.Default).Invoke(this, message, error);
        }
        catch (Exception e) {
            Peer.Log.LogError(e, "Send handler failed");
        }
    }
}
