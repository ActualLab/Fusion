namespace ActualLab.Rpc.Infrastructure;

// Base class for RPC transports. Serialization happens synchronously inside
// Write (protected by SemaphoreSlim), while actual sending is async.
public abstract class RpcTransport : IAsyncDisposable, IAsyncEnumerable<RpcInboundMessage>
{
    private volatile CancellationTokenSource? _stopCts;

    public CancellationToken StopToken { get; }
    public abstract Task WhenReadCompleted { get; }
    public abstract Task WhenWriteCompleted { get; }
    public abstract Task WhenClosed { get; }

    protected RpcTransport(CancellationToken cancellationToken)
    {
        _stopCts = cancellationToken.CreateLinkedTokenSource();
        StopToken = _stopCts.Token;
    }

    // Writes a message. Returns Task.CompletedTask if write succeeded synchronously,
    // otherwise returns a task to await. Throws ChannelClosedException if transport is completed.
    public abstract Task Write(RpcOutboundMessage message, CancellationToken cancellationToken = default);

    public abstract bool TryComplete(Exception? error = null);

    public ValueTask DisposeAsync()
    {
        var stopCts = Interlocked.Exchange(ref _stopCts, null);
        stopCts.CancelAndDisposeSilently();
        return WhenClosed.ToValueTask();
    }

    public abstract IAsyncEnumerator<RpcInboundMessage> GetAsyncEnumerator(CancellationToken cancellationToken = default);
}
