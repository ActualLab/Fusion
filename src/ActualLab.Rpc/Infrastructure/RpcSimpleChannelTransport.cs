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

    public RpcPeer Peer { get; }
    public RpcByteMessageSerializerV4 MessageSerializer { get; }
    public int InitialBufferCapacity { get; init; } = 256;
    public override Task WhenCompleted => _whenCompleted;
    public override Task WhenClosed { get; }

    public RpcSimpleChannelTransport(
        Channel<ArrayOwner<byte>> channel,
        RpcPeer peer,
        CancellationTokenSource? stopTokenSource = null)
        : base(stopTokenSource)
    {
        _reader = channel.Reader;
        _writer = channel.Writer;
        Peer = peer;
        MessageSerializer = new RpcByteMessageSerializerV4(peer);
        _whenCompleted = _whenCompletedSource.Task;
        WhenClosed = _whenCompletedSource.Task.SuppressExceptions();
    }

    protected override Task DisposeAsyncCore()
    {
        TryComplete();
        return Task.CompletedTask;
    }

    public override Task Write(RpcOutboundMessage message, CancellationToken cancellationToken = default)
    {
        if (_whenCompleted.IsCompleted)
            return Task.FromException(new ChannelClosedException());

        var buffer = new ArrayPoolBuffer<byte>(InitialBufferCapacity, mustClear: false);
        try {
            MessageSerializer.Write(buffer, message);
        }
        catch {
            buffer.Dispose();
            throw;
        }

        var frame = buffer.ToArrayOwnerAndDispose();
#pragma warning disable CA2025
        return _writer.TryWrite(frame)
            ? Task.CompletedTask
            : _writer.WriteAsync(frame, cancellationToken).AsTask();
#pragma warning restore CA2025
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

        try {
            await foreach (var frame in _reader.ReadAllAsync(commonCts.Token).ConfigureAwait(false)) {
                var message = MessageSerializer.Read(frame, 0, out _);
                yield return message;
                frame.Dispose(); // It's safe to dispose frame only at this point
            }
        }
        finally {
            _ = DisposeAsync();
        }
    }
}
