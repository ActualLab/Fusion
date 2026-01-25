using ActualLab.IO;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcSimpleChannelTransport : RpcTransport
{
    private readonly ChannelReader<ArrayOwner<byte>> _reader;
    private readonly ChannelWriter<ArrayOwner<byte>> _writer;
    private readonly TaskCompletionSource _writeCompletedSource = new();
    private int _getAsyncEnumeratorCounter;

    public RpcPeer Peer { get; }
    public RpcByteMessageSerializerV4 MessageSerializer { get; }
    public int InitialBufferCapacity { get; init; } = 256;
    public override Task WhenClosed { get; }

    public RpcSimpleChannelTransport(
        Channel<ArrayOwner<byte>> channel,
        RpcPeer peer,
        CancellationToken cancellationToken = default)
        : base(cancellationToken)
    {
        _reader = channel.Reader;
        _writer = channel.Writer;
        Peer = peer;
        MessageSerializer = new RpcByteMessageSerializerV4(peer);
        WhenClosed = _writeCompletedSource.Task.SuppressExceptions();
    }

    public override ValueTask DisposeAsync()
    {
        var result = base.DisposeAsync();
        TryComplete();
        return result;
    }

    public override Task Write(RpcOutboundMessage message, CancellationToken cancellationToken = default)
    {
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
        if (!_writer.TryComplete(error))
            return false;

        if (error is not null)
            _writeCompletedSource.TrySetException(error);
        else
            _writeCompletedSource.TrySetResult();
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
