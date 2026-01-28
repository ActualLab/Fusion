using ActualLab.IO;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcSimpleChannelTransport : RpcTransport
{
    private readonly ChannelReader<ArrayOwner<byte>> _reader;
    private readonly ChannelWriter<ArrayOwner<byte>> _writer;
    private readonly AsyncTaskMethodBuilder _whenCompletedSource = AsyncTaskMethodBuilderExt.New();
    private readonly Task _whenCompleted;
    private int _getAsyncEnumeratorCounter;

    public RpcMessageSerializer MessageSerializer { get; }
    public int InitialBufferCapacity { get; init; } = 256;
    public override Task WhenCompleted => _whenCompleted;
    public override Task WhenClosed { get; }

    public RpcSimpleChannelTransport(
        RpcPeer peer,
        Channel<ArrayOwner<byte>> channel,
        CancellationTokenSource? stopTokenSource = null)
        : base(peer, stopTokenSource)
    {
        _reader = channel.Reader;
        _writer = channel.Writer;
        MessageSerializer = peer.MessageSerializer;
        _whenCompleted = _whenCompletedSource.Task;
        WhenClosed = _whenCompletedSource.Task.SuppressExceptions();
    }

    protected override Task DisposeAsyncCore()
    {
        TryComplete();
        return Task.CompletedTask;
    }

    public override async Task Send(
        RpcOutboundMessage message,
        RpcSendErrorHandler errorHandler,
        CancellationToken cancellationToken = default)
    {
        try {
            if (_whenCompleted.IsCompleted)
                throw new ChannelClosedException();

            // No need to dispose the buffer, it's array is disposed by the receiving side via frame.Dispose there
            var buffer = new ArrayPoolBuffer<byte>(InitialBufferCapacity, mustClear: false);
            MessageSerializer.WriteFunc(buffer, message);
            var frame = new ArrayOwner<byte>(buffer.Pool, buffer.Array, buffer.WrittenCount);
            await _writer.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (errorHandler.Invoke(e, Peer, message, this)) {
            // Intended, errorHandler handled it
        }
    }

    public override bool TryComplete(Exception? error = null)
    {
        if (!_whenCompletedSource.TrySetFromResult(new Result<Unit>(default, error)))
            return false;

        _writer.TryComplete(error);
        return true;
    }

    public override IAsyncEnumerator<RpcInboundMessage> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => Interlocked.Increment(ref _getAsyncEnumeratorCounter) == 1
            ? ReadAllImpl(cancellationToken).GetAsyncEnumerator(cancellationToken)
            : throw ActualLab.Internal.Errors.AlreadyInvoked($"{GetType().GetName()}.GetAsyncEnumerator");

    // Private methods

    private async IAsyncEnumerable<RpcInboundMessage> ReadAllImpl([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var commonCts = cancellationToken.LinkWith(StopToken);

        var readFunc = MessageSerializer.ReadFunc;
        try {
            await foreach (var frame in _reader.ReadAllAsync(commonCts.Token).ConfigureAwait(false)) {
                var message = readFunc(frame.Memory, out _);
                yield return message;
                frame.Dispose(); // It's safe to dispose frame only at this point
            }
        }
        finally {
            _ = DisposeAsync();
        }
    }
}
