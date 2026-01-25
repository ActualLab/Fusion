using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using System.Threading.Channels;
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
        public int MaxPendingFrameCount { get; init; } = 8;
        public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(10);
    }

    private static readonly MeterSet StaticMeters = new();

    private readonly Channel<ArrayPoolArrayHandle<byte>> _frameChannel;
    private readonly ChannelWriter<ArrayPoolArrayHandle<byte>> _frameWriter;
    private readonly ConcurrentQueue<RpcOutboundMessage> _writeQueue = new();
    private readonly int _writeFrameSize;
    private readonly int _minReadBufferSize;
    private readonly MeterSet _meters = StaticMeters;
    private int _writerCount;
    private ArrayPoolBuffer<byte>? _writeBuffer;
    private volatile int _frameSenderIsIdle; // 1 = sender needs flush, 0 = sender is busy
    private volatile Task _whenWriteCompleted = Task.CompletedTask;
    private int _getAsyncEnumeratorCounter;

    public bool OwnsWebSocketOwner { get; set; } = true;
    public Options Settings { get; }
    public WebSocketOwner WebSocketOwner { get; }
    public WebSocket WebSocket { get; }
    public RpcPeer Peer { get; }
    public RpcByteMessageSerializerV4 MessageSerializer { get; }
    public ILogger? Log { get; }
    public ILogger? ErrorLog { get; }

    public Task WhenReadCompleted { get; }
    public Task WhenWriteCompleted { get; }
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
        _writeBuffer = new ArrayPoolBuffer<byte>(settings.MinWriteBufferSize, mustClear: false);
        _frameChannel = Channel.CreateBounded<ArrayPoolArrayHandle<byte>>(
            new BoundedChannelOptions(settings.MaxPendingFrameCount) {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait,
            });
        _frameWriter = _frameChannel.Writer;

        using var __ = ExecutionContextExt.TrySuppressFlow();
        WhenReadCompleted = TaskExt.NeverEnding(StopToken).SuppressCancellation();
        WhenWriteCompleted = Task.Run(RunFrameSender, CancellationToken.None);
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

    // Lock-free write: uses atomic _writerCount to determine who serializes.
    // Write primary (count==1) serializes messages and may flush buffer.
    public override Task Write(RpcOutboundMessage message, CancellationToken cancellationToken = default)
    {
        var count = Interlocked.Increment(ref _writerCount);
        if (count != 1) {
            message.PrepareWhenSerialized();
            _writeQueue.Enqueue(message);
            Interlocked.Decrement(ref _writerCount);
            return message.WhenSerialized;
        }

        var whenWriteCompleted = _whenWriteCompleted;
        return whenWriteCompleted.IsCompleted
            ? WriteAsPrimary(message)
            : WriteAsPrimary(message, whenWriteCompleted);
    }

    public override bool TryComplete(Exception? error = null)
    {
        if (!_frameWriter.TryComplete(error))
            return false;

        _ = Task.Run(async () => {
            // Wait to become write primary
            while (Interlocked.CompareExchange(ref _writerCount, 1, 0) != 0)
                await Task.Yield();

            // We're the primary writer now
            try {
                var writeBuffer = _writeBuffer;
                _writeBuffer = null;
                writeBuffer?.Dispose();
            }
            finally {
                Interlocked.Decrement(ref _writerCount);
            }
        }, CancellationToken.None);
        return true;
    }

    public override IAsyncEnumerator<RpcInboundMessage> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _getAsyncEnumeratorCounter) != 1)
            throw ActualLab.Internal.Errors.AlreadyInvoked($"{GetType().GetName()}.GetAsyncEnumerator");

        return ReadAllImpl(cancellationToken).GetAsyncEnumerator(cancellationToken);
    }

    // Private methods

    private async Task WriteAsPrimary(RpcOutboundMessage message, Task whenReadyToWrite)
    {
        await whenReadyToWrite.ConfigureAwait(false);
        await WriteAsPrimary(message).ConfigureAwait(false);
    }

    private Task WriteAsPrimary(RpcOutboundMessage? message)
    {
        try {
            var writeBuffer = _writeBuffer ?? throw new ChannelClosedException();
            if (message is not null)
                SerializeMessage(message, writeBuffer);
            while (_writeQueue.TryDequeue(out message)) {
                try {
                    SerializeMessage(message, writeBuffer);
                    message.CompleteWhenSerialized();
                }
                catch (Exception e) {
                    message.CompleteWhenSerialized(e);
                }
            }

            // Checking whether we need to flush the write buffer
            var bufferSize = writeBuffer.WrittenCount;
            if ((bufferSize < _writeFrameSize && _frameSenderIsIdle == 0) || bufferSize <= 0)
                return Task.CompletedTask;

            // Flushing write buffer
            var frame = writeBuffer.ResetAndReturnArrayHandle(Settings.MinWriteBufferSize);
            return _frameWriter.TryWrite(frame)
                ? Task.CompletedTask // Fast path
                : _whenWriteCompleted = _frameWriter.WriteAsync(frame).AsTask(); // Slow path
        }
        catch (Exception e) {
            // Drain the queue and complete all messages with an error
            while (_writeQueue.TryDequeue(out message))
                message.CompleteWhenSerialized(e);
            return Task.FromException(e);
        }
        finally {
            Interlocked.Decrement(ref _writerCount);
        }
    }

    private void SerializeMessage(RpcOutboundMessage message, ArrayPoolBuffer<byte> writeBuffer)
    {
        _meters.OutgoingItemCounter.Add(1);
        var startOffset = writeBuffer.WrittenCount;
        try {
            // Size prefix placeholder
            writeBuffer.GetSpan(64);
            writeBuffer.Advance(4);

            // Serialize message (serializer is embedded in message)
            MessageSerializer.Write(writeBuffer, message);

            // Write size prefix
            var size = writeBuffer.WrittenCount - startOffset;
            writeBuffer.WrittenSpan.WriteUnchecked(size, startOffset);
        }
        catch (Exception e) {
            writeBuffer.Position = startOffset;
            ErrorLog?.LogError(e, "Couldn't serialize the outbound message: {Message}", message);
            throw; // Re-throw so caller gets the error
        }
    }

    private async Task RunFrameSender()
    {
        var frameReader = _frameChannel.Reader;
        try {
            while (true) {
                Interlocked.Exchange(ref _frameSenderIsIdle, 1);

                // Try to steal any pending buffer data
                if (TryStealFrame(out var frame)) {
                    Interlocked.Exchange(ref _frameSenderIsIdle, 0);
                    await WebSocket
                        .SendAsync(frame.WrittenMemory, WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None)
                        .ConfigureAwait(false);
                    _meters.OutgoingFrameSizeHistogram.Record(frame.WrittenCount);
                    frame.Dispose(); // Dispose just returns it back to the pool, so fine to lose it on error
                    continue;
                }

                // Pull the frame from _frameChannel
                if (!await frameReader.WaitToReadAsync(StopToken).ConfigureAwait(false))
                    return; // frameReader completed

                Interlocked.Exchange(ref _frameSenderIsIdle, 0);
                while (frameReader.TryRead(out frame)) {
                    await WebSocket
                        .SendAsync(frame.WrittenMemory, WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None)
                        .ConfigureAwait(false);
                    _meters.OutgoingFrameSizeHistogram.Record(frame.WrittenCount);
                    frame.Dispose(); // Dispose just returns it back to the pool, so fine to lose it on error
                }
            }
        }
        catch (Exception e) when (e is not OperationCanceledException) {
            TryComplete(e);
            _ = DisposeAsync();
        }
    }

    private bool TryStealFrame(out ArrayPoolArrayHandle<byte> frame)
    {
        frame = default!;

        // Try to become write primary
        if (Interlocked.CompareExchange(ref _writerCount, 1, 0) != 0)
            return false; // Writers are active, they'll flush

        // We're the primary writer now
        try {
            if (_writeBuffer is not { } writeBuffer || writeBuffer.WrittenCount == 0)
                return false;

            frame = writeBuffer.ResetAndReturnArrayHandle(Settings.MinWriteBufferSize);
            return true;
        }
        finally {
            Interlocked.Decrement(ref _writerCount);
        }
    }

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

                var frame = readBuffer.ResetAndReturnArrayHandle(_minReadBufferSize);
                var offset = 0;
                while (offset < totalLength) {
                    var message = TryDeserialize(frame, ref offset, totalLength);
                    if (message is not null)
                        yield return message;
                }
                // The code that uses frame's data (RpcInboundMessage.ArgumentData) is running synchronously,
                // so if we're here, the frame's array can return to the pool.
                frame.Dispose();
            }
        }
        finally {
            readBuffer.Dispose();
            _ = DisposeAsync();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private RpcInboundMessage? TryDeserialize(ArrayPoolArrayHandle<byte> frame, ref int offset, int totalLength)
    {
        _meters.IncomingItemCounter.Add(1);
        int size = 0;
        bool isSizeValid = false;
        try {
            var array = frame.Array;
            size = array.AsSpan(offset).ReadUnchecked<int>();
            isSizeValid = size > 0 && offset + size <= totalLength;
            if (!isSizeValid)
                throw Errors.InvalidItemSize();

            // Read message - ArgumentData is a projection into our buffer (zero-copy)
            var inboundMessage = MessageSerializer.Read(frame, offset + sizeof(int), out int readSize);
            if (readSize != size - sizeof(int))
                throw Errors.InvalidItemSize();

            offset += size;
            return inboundMessage;
        }
        catch (Exception e) {
            var remaining = frame.Array.AsMemory(offset, totalLength - offset);
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
