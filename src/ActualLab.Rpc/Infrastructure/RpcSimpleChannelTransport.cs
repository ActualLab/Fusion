using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// An <see cref="RpcTransport"/> backed by simple in-memory channels, used for loopback connections.
/// </summary>
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
    public Task WhenClosed { get; }

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

    public override void Send(RpcOutboundMessage message, CancellationToken cancellationToken = default)
    {
        if (_whenCompleted.IsCompleted) {
            CompleteSend(message, new ChannelClosedException());
            return;
        }

        ArrayPoolBuffer<byte>? buffer = null;
        ArrayOwner<byte> frame;
        try {
            buffer = new ArrayPoolBuffer<byte>(
                ArrayPools.SharedBytePool, InitialBufferCapacity, mustClear: false);
            MessageSerializer.WriteFunc(buffer, message);
            frame = buffer.ToArrayOwnerAndDispose();
        }
        catch (Exception e) {
            buffer?.Dispose();
            CompleteSend(message, e);
            return;
        }

        bool isWritten;
        try {
            isWritten = _writer.TryWrite(frame);
        }
        catch (Exception e) {
            frame.Dispose();
            CompleteSend(message, e);
            return;
        }
        if (isWritten) {
            CompleteSend(message);
            return;
        }

#pragma warning disable CA2025
        _ = Write(frame, message, cancellationToken);
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

    private async Task Write(
        ArrayOwner<byte> frame,
        RpcOutboundMessage message,
        CancellationToken cancellationToken)
    {
        Exception? error = null;
        try {
            await _writer.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) {
            frame.Dispose();
            error = e;
        }

        if (error is null)
            CompleteSend(message);
        else
            CompleteSend(message, error);
    }

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
