using System.Buffers;
using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using ActualLab.IO;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.WebSockets;

public sealed class WebSocketRpcTransport : RpcTransport
{
    public record Options
    {
        public static readonly Options Default = new();

        public int WriteFrameSize { get; init; } = 12_000; // 8 x 1500 (min. MTU) minus some reserve
        public int MinWriteBufferSize { get; init; } = 24_000;
        public int MinReadBufferSize { get; init; } = 24_000;
        public int RetainedBufferSize { get; init; } = 120_000;
        public int BufferRenewPeriod { get; init; } = 100;
        public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(10);
    }

    private static readonly MeterSet StaticMeters = new();

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly TaskCompletionSource _writeCompletedTcs = new();
    private readonly int _writeFrameSize;
    private readonly int _minReadBufferSize;
    private readonly int _retainedBufferSize;
    private readonly int _bufferRenewPeriod;
    private readonly MeterSet _meters = StaticMeters;
    private readonly ArrayPoolBuffer<byte> _writeBuffer;
    private int _writeBufferResetCounter;
    private int _pendingWriterCount;
    private bool _isSending;
    private int _getAsyncEnumeratorCounter;
    private volatile bool _isCompleted;

    public bool OwnsWebSocketOwner { get; set; } = true;
    public Options Settings { get; }
    public WebSocketOwner WebSocketOwner { get; }
    public WebSocket WebSocket { get; }
    public RpcPeer Peer { get; }
    public RpcByteMessageSerializerV4 MessageSerializer { get; }
    public ILogger? Log { get; }
    public ILogger? ErrorLog { get; }

    public override Task WhenReadCompleted { get; }
    public override Task WhenWriteCompleted => _writeCompletedTcs.Task;
    public override Task WhenClosed { get; }

    public WebSocketRpcTransport(
        Options settings,
        WebSocketOwner webSocketOwner,
        RpcPeer peer,
        CancellationToken cancellationToken = default)
        : base(cancellationToken)
    {
        Settings = settings;
        WebSocketOwner = webSocketOwner;
        WebSocket = webSocketOwner.WebSocket;
        Peer = peer;
        MessageSerializer = new RpcByteMessageSerializerV4(peer);

        Log = webSocketOwner.Services.LogFor(GetType());
        ErrorLog = Log.IfEnabled(LogLevel.Error);

        _writeFrameSize = settings.WriteFrameSize;
        if (_writeFrameSize <= 0)
            throw new ArgumentOutOfRangeException($"{nameof(settings)}.{nameof(settings.WriteFrameSize)} must be positive.");
        _minReadBufferSize = settings.MinReadBufferSize;
        _retainedBufferSize = settings.RetainedBufferSize;
        _bufferRenewPeriod = settings.BufferRenewPeriod;
        _writeBuffer = new ArrayPoolBuffer<byte>(settings.MinWriteBufferSize, false);

        using var _ = ExecutionContextExt.TrySuppressFlow();
        WhenReadCompleted = Task.Run(() => TaskExt.NeverEnding(StopToken), default);
        WhenClosed = Task.Run(async () => {
            Interlocked.Increment(ref _meters.ChannelCount);
            try {
                await Task.WhenAny(WhenReadCompleted, WhenWriteCompleted).SilentAwait(false);
                await CloseWebSocket(null).SilentAwait(false);
                await WhenReadCompleted.SilentAwait(false);
                await WhenWriteCompleted.SilentAwait(false);
                if (OwnsWebSocketOwner)
                    await WebSocketOwner.DisposeAsync().ConfigureAwait(false);
            }
            finally {
                Interlocked.Decrement(ref _meters.ChannelCount);
            }
        }, default);
    }

    // Writes a message. Returns Task.CompletedTask if write succeeded synchronously,
    // otherwise returns a task to await. Uses batching: buffers while sending or while other writers are waiting.
    public override Task Write(RpcOutboundMessage message, CancellationToken cancellationToken = default)
    {
        if (_isCompleted)
            throw new ChannelClosedException();

        Interlocked.Increment(ref _pendingWriterCount);

        // Fast path: try to acquire lock synchronously
        if (_writeLock.Wait(0)) {
            try {
                if (!SerializeFromWriteLock(message))
                    throw new ChannelClosedException();

                MaybeStartSendFromWriteLock();
            }
            finally {
                _writeLock.Release();
                // If we're the last writer, ensure a send is triggered if needed
                if (Interlocked.Decrement(ref _pendingWriterCount) == 0)
                    MaybeStartSendOnLastWriter();
            }
            return Task.CompletedTask;
        }

        // Slow path: wait for lock asynchronously
        return WriteSlowPath(message, cancellationToken);
    }

    private async Task WriteSlowPath(RpcOutboundMessage message, CancellationToken cancellationToken)
    {
        try {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                if (!SerializeFromWriteLock(message))
                    throw new ChannelClosedException();

                MaybeStartSendFromWriteLock();
            }
            finally {
                _writeLock.Release();
            }
        }
        finally {
            // If we're the last writer, ensure a send is triggered if needed
            if (Interlocked.Decrement(ref _pendingWriterCount) == 0)
                MaybeStartSendOnLastWriter();
        }
    }

    public override bool TryComplete(Exception? error = null)
    {
        if (_isCompleted)
            return false;
        _isCompleted = true;

        // Complete the write side
        if (error is not null)
            _writeCompletedTcs.TrySetException(error);
        else
            _writeCompletedTcs.TrySetResult();

        return true;
    }

    private bool SerializeFromWriteLock(RpcOutboundMessage message)
    {
        if (_isCompleted)
            return false;

        _meters.OutgoingItemCounter.Add(1);
        var startOffset = _writeBuffer.WrittenCount;
        try {
            // Size prefix placeholder
            _writeBuffer.GetSpan(64);
            _writeBuffer.Advance(4);

            // Serialize message (serializer is embedded in message)
            MessageSerializer.Write(_writeBuffer, message);

            // Write size prefix
            var size = _writeBuffer.WrittenCount - startOffset;
            _writeBuffer.WrittenSpan.WriteUnchecked(size, startOffset);

            return true;
        }
        catch (Exception e) {
            _writeBuffer.Position = startOffset;
            ErrorLog?.LogError(e, "Couldn't serialize the outbound message: {Message}", message);
            throw; // Re-throw so caller gets the error
        }
    }

    private void MaybeStartSendFromWriteLock()
    {
        if (_writeBuffer.WrittenCount == 0)
            return;

        // Send if: buffer large enough OR (last pending writer AND not currently sending)
        var pendingWriters = Volatile.Read(ref _pendingWriterCount);
        var bufferLargeEnough = _writeBuffer.WrittenCount >= _writeFrameSize;
        var lastWriterAndIdle = pendingWriters <= 1 && !_isSending;

        if (!bufferLargeEnough && !lastWriterAndIdle)
            return; // Keep buffering - either sending or more writers coming

        // Take the buffer data and start send
        var data = _writeBuffer.WrittenMemory.ToArray();
        ResetWriteBufferFromWriteLock();
        _isSending = true;

        // Fire and forget the send loop
        _ = RunSendLoop(data);
    }

    // Called after decrementing pendingWriterCount to 0 - handles the race where
    // send loop saw pending writers and didn't steal, but now all writers are done.
    private void MaybeStartSendOnLastWriter()
    {
        // Quick check without lock - if already sending or nothing to send, skip
        if (_isSending || _writeBuffer.WrittenCount == 0)
            return;

        // Try to acquire lock - if can't, someone else is handling it
        if (!_writeLock.Wait(0))
            return;

        try {
            // Double-check conditions under lock
            if (_isSending || _writeBuffer.WrittenCount == 0)
                return;

            // Start send
            var data = _writeBuffer.WrittenMemory.ToArray();
            ResetWriteBufferFromWriteLock();
            _isSending = true;
            _ = RunSendLoop(data);
        }
        finally {
            _writeLock.Release();
        }
    }

    // Sends data, then tries to steal more from buffer if no writers are waiting
    private async Task RunSendLoop(ReadOnlyMemory<byte> data)
    {
        try {
            while (true) {
                await WebSocket
                    .SendAsync(data, WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None)
                    .ConfigureAwait(false);
                _meters.OutgoingFrameSizeHistogram.Record(data.Length);

                // Try to steal more data from buffer
                await _writeLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                try {
                    // Steal if: no pending writers AND there's data in buffer
                    if (Volatile.Read(ref _pendingWriterCount) == 0 && _writeBuffer.WrittenCount > 0) {
                        data = _writeBuffer.WrittenMemory.ToArray();
                        ResetWriteBufferFromWriteLock();
                        // Continue loop to send stolen data
                    }
                    else {
                        // Either writers are active (let them handle next send) or buffer is empty
                        _isSending = false;
                        return;
                    }
                }
                finally {
                    _writeLock.Release();
                }
            }
        }
        catch (Exception e) {
            // Mark as not sending and complete with error
            await _writeLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try {
                _isSending = false;
            }
            finally {
                _writeLock.Release();
            }
            TryComplete(e);
            _writeBuffer.Dispose();
            _ = DisposeAsync();
        }
    }

    private void ResetWriteBufferFromWriteLock()
    {
        if (MustRenewBuffer(ref _writeBufferResetCounter))
            _writeBuffer.Renew(Settings.MinWriteBufferSize, _retainedBufferSize);
        else
            _writeBuffer.Reset();
    }

    public override IAsyncEnumerator<RpcInboundMessage> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _getAsyncEnumeratorCounter) != 1)
            throw ActualLab.Internal.Errors.AlreadyInvoked($"{GetType().GetName()}.GetAsyncEnumerator");

        return ReadAllImpl(cancellationToken).GetAsyncEnumerator(cancellationToken);
    }

    // Private methods

    private async IAsyncEnumerable<RpcInboundMessage> ReadAllImpl([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var commonCts = cancellationToken.LinkWith(StopToken);
        using var commonTokenRegistration = commonCts.Token.Register(() => _ = DisposeAsync());

        // Start with a non-pooled buffer for initial reads
        var readBuffer = new ArrayPoolBuffer<byte>(_minReadBufferSize, false);

        try {
            while (true) {
                var readMemory = readBuffer.GetMemory(_minReadBufferSize);
                var arraySegment = new ArraySegment<byte>(readBuffer.Array, readBuffer.WrittenCount, readMemory.Length);
                WebSocketReceiveResult r;
                try {
                    r = await WebSocket.ReceiveAsync(arraySegment, CancellationToken.None).ConfigureAwait(false);
                }
                catch (WebSocketException e) when (e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
                    r = new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
                }
                if (r.MessageType == WebSocketMessageType.Close)
                    yield break;
                if (r.MessageType != WebSocketMessageType.Binary)
                    throw Errors.InvalidWebSocketMessageType(r.MessageType, WebSocketMessageType.Binary);

                readBuffer.Advance(r.Count);
                _meters.IncomingFrameSizeHistogram.Record(r.Count);
                if (!r.EndOfMessage)
                    continue; // Continue reading into the same buffer

                var totalLength = readBuffer.WrittenCount;

                // Create ref-counted buffer, takes ownership of readBuffer's array
                var holder = readBuffer.ReplaceAndReturnArrayHandle(_minReadBufferSize);

                // Transport holds a ref while parsing
                var transportRef = holder.NewRef();

                // Parse all messages from this buffer
                var offset = 0;
                while (offset < totalLength) {
                    var message = TryDeserialize(holder, ref offset, totalLength);
                    if (message is not null)
                        yield return message;
                }

                // Release transport's reference; buffer returns to pool when all messages are disposed
                transportRef.Dispose();

                // Get a new buffer for the next read
                readBuffer = new ArrayPoolBuffer<byte>(_minReadBufferSize, false);
            }
        }
        finally {
            readBuffer.Dispose();
            _ = DisposeAsync();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private RpcInboundMessage? TryDeserialize(ArrayPoolArrayHandle<byte> buffer, ref int offset, int totalLength)
    {
        _meters.IncomingItemCounter.Add(1);
        int size = 0;
        bool isSizeValid = false;
        try {
            var array = buffer.Array!;
            size = array.AsSpan(offset).ReadUnchecked<int>();
            isSizeValid = size > 0 && offset + size <= totalLength;
            if (!isSizeValid)
                throw Errors.InvalidItemSize();

            // Read message - ArgumentData is a projection into our buffer (zero-copy)
            var inboundMessage = MessageSerializer.Read(buffer, offset + sizeof(int), out int readSize);
            if (readSize != size - sizeof(int))
                throw Errors.InvalidItemSize();

            offset += size;
            return inboundMessage;
        }
        catch (Exception e) {
            var remaining = buffer.Array.AsMemory(offset, totalLength - offset);
            ErrorLog?.LogError(e, "Couldn't deserialize: {Data}", new TextOrBytes(DataFormat.Bytes, remaining));
            offset = isSizeValid ? offset + size : totalLength;
            return null;
        }
    }

    private async Task CloseWebSocket(Exception? error)
    {
        if (error is OperationCanceledException)
            error = null;

        var status = WebSocketCloseStatus.NormalClosure;
        var message = "Ok.";
        if (error is not null) {
            status = WebSocketCloseStatus.InternalServerError;
            message = "Internal Server Error.";
            Log?.LogInformation(error, "WebSocket is closing after an error");
        }
        if (WebSocket.State is WebSocketState.Closed or WebSocketState.Aborted)
            return;

        try {
            await WebSocket.CloseAsync(status, message, default)
                .WaitAsync(Settings.CloseTimeout, CancellationToken.None)
                .SilentAwait(false);
        }
        catch {
            // Intended
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MustRenewBuffer(ref int counter)
    {
        if (++counter < _bufferRenewPeriod)
            return false;

        counter = 0;
        return true;
    }

    // Nested types

    public class MeterSet
    {
        public readonly ObservableCounter<long> ChannelCounter;
        public readonly Counter<long> IncomingItemCounter;
        public readonly Counter<long> OutgoingItemCounter;
        public readonly Histogram<int> IncomingFrameSizeHistogram;
        public readonly Histogram<int> OutgoingFrameSizeHistogram;
        public long ChannelCount;

        public MeterSet()
        {
            var m = RpcInstruments.Meter;
            var ms = "rpc.ws.transport";
            ChannelCounter = m.CreateObservableCounter($"{ms}.count",
                () => Interlocked.Read(ref ChannelCount),
                null, "Number of WebSocketRpcTransport instances.");
            IncomingItemCounter = m.CreateCounter<long>($"{ms}.incoming.item.count",
                null, "Number of items received via WebSocketRpcTransport.");
            OutgoingItemCounter = m.CreateCounter<long>($"{ms}.outgoing.item.count",
                null, "Number of items sent via WebSocketRpcTransport.");
            IncomingFrameSizeHistogram = m.CreateHistogram<int>($"{ms}.incoming.frame.size",
                "By", "WebSocketRpcTransport's incoming frame size in bytes.");
            OutgoingFrameSizeHistogram = m.CreateHistogram<int>($"{ms}.outgoing.frame.size",
                "By", "WebSocketRpcTransport's outgoing frame size in bytes.");
        }
    }
}
