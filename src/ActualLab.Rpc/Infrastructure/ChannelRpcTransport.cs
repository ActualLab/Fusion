using System.Buffers;
using ActualLab.IO;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc.Infrastructure;

public sealed class ChannelRpcTransport : RpcTransport
{
    private readonly ChannelReader<ArrayOwner<byte>> _reader;
    private readonly ChannelWriter<ArrayOwner<byte>> _writer;
    private readonly TaskCompletionSource _readCompletedSource = new();
    private readonly TaskCompletionSource _writeCompletedTcs = new();
    private volatile bool _isCompleted;
    private int _getAsyncEnumeratorCounter;

    public RpcPeer Peer { get; }
    public RpcByteMessageSerializerV4 MessageSerializer { get; }
    public Task WhenReadCompleted => _readCompletedSource.Task;
    public Task WhenWriteCompleted => _writeCompletedTcs.Task;
    public override Task WhenClosed { get; }

    public ChannelRpcTransport(
        Channel<ArrayOwner<byte>> channel,
        RpcPeer peer,
        CancellationToken cancellationToken = default)
        : base(cancellationToken)
    {
        _reader = channel.Reader;
        _writer = channel.Writer;
        Peer = peer;
        MessageSerializer = new RpcByteMessageSerializerV4(peer);

        WhenClosed = Task.WhenAll(WhenReadCompleted, WhenWriteCompleted);

        // Complete read when channel reader completes
        _ = channel.Reader.Completion.ContinueWith(
            t => {
                if (t.IsFaulted)
                    _readCompletedSource.TrySetException(t.Exception!.InnerExceptions);
                else if (t.IsCanceled)
                    _readCompletedSource.TrySetCanceled();
                else
                    _readCompletedSource.TrySetResult();
            },
            TaskScheduler.Default);
    }

    public override Task Write(RpcOutboundMessage message, CancellationToken cancellationToken = default)
    {
        if (_isCompleted)
            throw new ChannelClosedException();

        var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
        try {
            MessageSerializer.Write(buffer, message);
        }
        catch {
            buffer.Dispose();
            throw;
        }

        // ToArrayOwnerAndReset steals the array and gives buffer a new one
        var frame = buffer.ToArrayOwnerAndReset(16);
        buffer.Dispose(); // Dispose the new (empty) buffer array

        // Write frame to channel (frame is disposed by the reader after deserialization)
#pragma warning disable CA2025 // Frame ownership is transferred to reader
        return _writer.TryWrite(frame)
            ? Task.CompletedTask
            : WriteSlowPath(frame, cancellationToken);
#pragma warning restore CA2025
    }

    private async Task WriteSlowPath(ArrayOwner<byte> frame, CancellationToken cancellationToken)
    {
        try {
            await _writer.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        catch {
            frame.Dispose();
            throw;
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
            _readCompletedSource.TrySetResult();
            _ = DisposeAsync();
        }
    }
}
