using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc.Infrastructure;

public sealed class ChannelRpcTransport : RpcTransport
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ChannelReader<RpcInboundMessage> _reader;
    private readonly ChannelWriter<RpcInboundMessage> _writer;
    private readonly TaskCompletionSource _readCompletedTcs = new();
    private readonly TaskCompletionSource _writeCompletedTcs = new();
    private volatile bool _isCompleted;
    private int _getAsyncEnumeratorCounter;

    public RpcPeer? Peer { get; }

    public override Task WhenReadCompleted => _readCompletedTcs.Task;
    public override Task WhenWriteCompleted => _writeCompletedTcs.Task;
    public override Task WhenClosed { get; }

    public ChannelRpcTransport(
        Channel<RpcInboundMessage> channel,
        RpcPeer? peer = null,
        CancellationToken cancellationToken = default)
        : base(cancellationToken)
    {
        _reader = channel.Reader;
        _writer = channel.Writer;
        Peer = peer;

        WhenClosed = Task.WhenAll(WhenReadCompleted, WhenWriteCompleted);

        // Complete write when channel writer completes
        _ = channel.Reader.Completion.ContinueWith(
            t => {
                if (t.IsFaulted)
                    _readCompletedTcs.TrySetException(t.Exception!.InnerExceptions);
                else if (t.IsCanceled)
                    _readCompletedTcs.TrySetCanceled();
                else
                    _readCompletedTcs.TrySetResult();
            },
            TaskScheduler.Default);
    }

    public override Task Write(RpcOutboundMessage message, CancellationToken cancellationToken = default)
    {
        if (_isCompleted)
            throw new ChannelClosedException();

        // Fast path: try to acquire lock synchronously
        if (_writeLock.Wait(0)) {
            try {
                if (!WriteCore(message))
                    throw new ChannelClosedException();
            }
            finally {
                _writeLock.Release();
            }
            return Task.CompletedTask;
        }

        // Slow path: wait for lock asynchronously
        return WriteSlowPath(message, cancellationToken);
    }

    private async Task WriteSlowPath(RpcOutboundMessage message, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (!WriteCore(message))
                throw new ChannelClosedException();
        }
        finally {
            _writeLock.Release();
        }
    }

    public override bool TryComplete(Exception? error = null)
    {
        if (_isCompleted)
            return false;
        _isCompleted = true;

        var result = _writer.TryComplete(error);
        if (error is not null)
            _writeCompletedTcs.TrySetException(error);
        else
            _writeCompletedTcs.TrySetResult();

        return result;
    }

    public override IAsyncEnumerator<RpcInboundMessage> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _getAsyncEnumeratorCounter) != 1)
            throw ActualLab.Internal.Errors.AlreadyInvoked($"{GetType().GetName()}.GetAsyncEnumerator");

        return ReadAllImpl(cancellationToken).GetAsyncEnumerator(cancellationToken);
    }

    // Private methods

    private bool WriteCore(RpcOutboundMessage message)
    {
        if (_isCompleted)
            return false;

        // Set context for types that need it during serialization (e.g., RpcStream)
        var oldContext = RpcOutboundContext.Current;
        RpcOutboundContext.Current = message.Context;
        try {
            // Serialize arguments if needed
            var argumentData = message.ArgumentData;
            if (argumentData.IsEmpty && message.Arguments is not null && message.ArgumentSerializer is not null) {
                var buffer = RpcArgumentSerializer.GetWriteBuffer();
                message.ArgumentSerializer.Serialize(message.Arguments, message.NeedsPolymorphism, buffer);
                argumentData = RpcArgumentSerializer.GetWriteBufferMemory(buffer);
            }

            // Create RpcInboundMessage and write to channel
            var inboundMessage = new RpcInboundMessage(
                message.MethodDef.CallType.Id,
                message.RelatedId,
                message.MethodDef.Ref,
                argumentData,
                message.Headers,
                default);

            return _writer.TryWrite(inboundMessage);
        }
        finally {
            RpcOutboundContext.Current = oldContext;
        }
    }

    private async IAsyncEnumerable<RpcInboundMessage> ReadAllImpl([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var commonCts = cancellationToken.LinkWith(StopToken);

        try {
            await foreach (var message in _reader.ReadAllAsync(commonCts.Token).ConfigureAwait(false))
                yield return message;
        }
        finally {
            _readCompletedTcs.TrySetResult();
            _ = DisposeAsync();
        }
    }
}
