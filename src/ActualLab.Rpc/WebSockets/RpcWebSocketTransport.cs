using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using ActualLab.Channels;
using ActualLab.IO;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.WebSockets;

public sealed class RpcWebSocketTransport : RpcTransport
{
    public record Options
    {
        public static readonly Options Default = new();

        public Func<RpcFrameDelayer?>? FrameDelayerFactory { get; init; } = RpcFrameDelayerFactories.None;

        public int FrameSize { get; init; } = 12_000; // 8 x 1500 (min. MTU minus some reserve)
        public int MinReadBufferSize { get; init; } = 16_000;
        public int MaxReadBufferSize { get; init; } = 256_000;
        public int MinWriteBufferSize { get; init; } = 16_000;
        public int MaxWriteBufferSize { get; init; } = 256_000;
        public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(10);

        // Use of UnboundedChannelOptions is totally fine here: if the message is enqueued
        public ChannelOptions WriteChannelOptions { get; init; } = new UnboundedChannelOptions() {
            // FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true, // Must be true
            SingleWriter = false, // Must be false
            AllowSynchronousContinuations = false, // Must be false, setting it to true will kill the throughput!
        };
    }

    private delegate RpcInboundMessage? TryDeserializeMessageFunc(byte[] array, ref int offset, int totalLength);
    private delegate void SerializeMessageFunc(RpcOutboundMessage message, ArrayPoolBuffer<byte> buffer);

    // Text message delimiters (matches master branch WebSocketChannelImpl)
    private const byte LineFeed = 0x0A; // LF
    private const byte RecordSeparator = 0x1E; // RS

    private static readonly MeterSet StaticMeters = new();

    private readonly MeterSet _meters = StaticMeters;
    private readonly int _writeFrameSize;
    private readonly Channel<RpcOutboundMessage> _writeChannel;
    private readonly ChannelWriter<RpcOutboundMessage> _writeChannelWriter;
    private readonly AsyncTaskMethodBuilder _whenCompletedSource;
    private readonly Task _whenCompleted;
    private readonly RpcFrameDelayer? _frameDelayer;
    private ArrayPoolBuffer<byte> _writeBuffer;
    private ArrayPoolBuffer<byte> _flushingBuffer;
    private int _getAsyncEnumeratorCounter;

    public bool OwnsWebSocketOwner { get; set; } = true;
    public Options Settings { get; }
    public WebSocketOwner WebSocketOwner { get; }
    public WebSocket WebSocket { get; }
    public RpcPeer Peer { get; }
    public RpcMessageSerializer MessageSerializer { get; }
    public bool IsTextSerializer { get; }
    public WebSocketMessageType MessageType { get; }
    public ILogger? Log { get; }
    public ILogger? ErrorLog { get; }

    public override Task WhenCompleted => _whenCompleted;
    public override Task WhenClosed { get; }

    public RpcWebSocketTransport(
        Options settings,
        WebSocketOwner webSocketOwner,
        RpcPeer peer,
        CancellationTokenSource? stopTokenSource = null)
        : base(stopTokenSource)
    {
        Settings = settings;
        WebSocketOwner = webSocketOwner;
        WebSocket = webSocketOwner.WebSocket;
        Peer = peer;
        MessageSerializer = peer.MessageSerializer;
        IsTextSerializer = MessageSerializer is RpcTextMessageSerializer;
        MessageType = IsTextSerializer
            ? WebSocketMessageType.Text
            : WebSocketMessageType.Binary;

        Log = webSocketOwner.Services.LogFor(GetType());
        ErrorLog = Log.IfEnabled(LogLevel.Error);

        _whenCompletedSource = AsyncTaskMethodBuilderExt.New();
        _whenCompleted = _whenCompletedSource.Task;

        _writeFrameSize = settings.FrameSize;
        if (_writeFrameSize <= 0)
            throw new ArgumentOutOfRangeException($"{nameof(settings)}.{nameof(settings.FrameSize)} must be positive.");

        _frameDelayer = settings.FrameDelayerFactory?.Invoke();
        _writeBuffer = new ArrayPoolBuffer<byte>(Settings.MinWriteBufferSize, mustClear: false);
        _flushingBuffer = new ArrayPoolBuffer<byte>(Settings.MinWriteBufferSize, mustClear: false);

        _writeChannel = ChannelExt.Create<RpcOutboundMessage>(settings.WriteChannelOptions);
        _writeChannelWriter = _writeChannel.Writer;

        using var __ = ExecutionContextExt.TrySuppressFlow();
        WhenClosed = Task.Run(async () => {
            Interlocked.Increment(ref _meters.ChannelCount);
            try {
                var whenStopped = TaskExt.NeverEnding(StopToken);
                var whenWriterCompleted = Task.Run(RunWriter, CancellationToken.None);
                await Task.WhenAny(whenStopped, _whenCompleted, whenWriterCompleted).SilentAwait(false);

                // Stop everything
                StopTokenSource.Cancel(); // Stops writer loop (and reader loop)
                TryComplete(); // Stops writes
                await whenWriterCompleted.ConfigureAwait(false); // RunWriter never throws
                await _whenCompleted.SilentAwait(false); // Can fail, so we use SilentAwait here

                // Drain remaining pending messages (if any)
                while (_writeChannel.Reader.TryRead(out var message))
                    message.CompleteWhenSerialized(new ChannelClosedException());

                await CloseWebSocket(null).ConfigureAwait(false); // CloseWebSocket never throws

                // It's safer to dispose the buffers here rather than in 'finally',
                // coz if something fails and they're somehow still used,
                // we simply won't return them back to the pool, so GC will take care of them.
                _flushingBuffer.Dispose();
                _writeBuffer.Dispose();
            }
            catch (Exception e) {
                Log.LogError(e, "Error in WebSocketRpcTransport.WhenClosed, this should never happen");
            }
            finally {
                Interlocked.Decrement(ref _meters.ChannelCount);
            }
        }, default);
    }

    protected override async Task DisposeAsyncCore()
    {
        await WhenClosed.ConfigureAwait(false);
        if (OwnsWebSocketOwner)
            await WebSocketOwner.DisposeAsync().ConfigureAwait(false);
    }

    public override Task Write(RpcOutboundMessage message, CancellationToken cancellationToken = default)
    {
        var whenSerialized = message.WhenSerialized;

        // Fast path: since _writeChannel is typically an UnboundedChannel,
        // TryWrite always completes successfully while the channel is operational.
        if (_writeChannelWriter.TryWrite(message))
            return whenSerialized ?? Task.CompletedTask;

        // Slow path
        var writeTask = _writeChannelWriter.WriteAsync(message, cancellationToken);
        if (whenSerialized is null)
            return writeTask.AsTask(); // writeTask may throw ChannelClosedException or OperationCanceledException

        _ = CompleteAsync(message, writeTask);
        return whenSerialized;

        static async Task CompleteAsync(RpcOutboundMessage message, ValueTask writeTask) {
            try {
                await writeTask.ConfigureAwait(false);
                message.CompleteWhenSerialized();
            }
            catch (Exception e) { // writeTask may throw ChannelClosedException or OperationCanceledException
                message.CompleteWhenSerialized(e);
            }
        }
    }

    public override bool TryComplete(Exception? error = null)
    {
        if (!_writeChannelWriter.TryComplete(error))
            return false;

        _whenCompletedSource.TrySetFromResult(new Result<Unit>(default, error));
        return true;
    }

    public override IAsyncEnumerator<RpcInboundMessage> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => Interlocked.Increment(ref _getAsyncEnumeratorCounter) == 1
            ? ReadAllImpl(cancellationToken).GetAsyncEnumerator(cancellationToken)
            : throw ActualLab.Internal.Errors.AlreadyInvoked($"{GetType().GetName()}.GetAsyncEnumerator");

    // Private methods

    // This method should never throw
    private async Task RunWriter()
    {
        Exception? error = null;
        Task lastFlushTask = Task.CompletedTask;
        try {
            if (_frameDelayer is { } frameDelayer) {
                await RunWriterWithFrameDelayer(frameDelayer).ConfigureAwait(false);
                return;
            }

            var reader = _writeChannel.Reader;
            var serializeMessage = IsTextSerializer
                ? (SerializeMessageFunc)SerializeText
                : SerializeBinary;
            while (await reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false)) {
                while (reader.TryRead(out var message)) {
                    try {
                        serializeMessage(message, _writeBuffer);
                        message.CompleteWhenSerialized();
                    }
                    catch (Exception e) {
                        message.CompleteWhenSerialized(e);
                    }
                    if (_writeBuffer.WrittenCount >= _writeFrameSize) {
                        await lastFlushTask.ConfigureAwait(false);
                        lastFlushTask = FlushFrame();
                    }
                }

                // Final flush before await
                if (_writeBuffer.WrittenCount != 0) {
                    await lastFlushTask.ConfigureAwait(false);
                    lastFlushTask = FlushFrame();
                }
            }
            // Await the last flush
            await lastFlushTask.ConfigureAwait(false);
        }
        catch (Exception e) {
            if (!e.IsCancellationOf(StopToken))
                error = e;
        }
        finally {
            TryComplete(error);
        }
    }

    private async Task RunWriterWithFrameDelayer(RpcFrameDelayer frameDelayer)
    {
        Task? whenMustFlush = null; // null = no flush required / nothing to flush
        Task lastFlushTask = Task.CompletedTask;
        Task<bool>? waitToReadTask = null;
        var reader = _writeChannel.Reader;
        var serialize = IsTextSerializer
            ? (SerializeMessageFunc)SerializeText
            : SerializeBinary;

        while (true) {
            // When we are here, the sync read part is completed, so WaitToReadAsync will likely await.
            if (whenMustFlush is not null) {
                if (whenMustFlush.IsCompleted) {
                    // Flush is required right now.
                    if (_writeBuffer.WrittenCount != 0) {
                        await lastFlushTask.ConfigureAwait(false);
                        lastFlushTask = FlushFrame();
                    }
                    whenMustFlush = null;
                }
                else {
                    // Flush is pending.
                    waitToReadTask ??= reader.WaitToReadAsync(CancellationToken.None).AsTask();
                    await Task.WhenAny(whenMustFlush, waitToReadTask).ConfigureAwait(false);
                    if (!waitToReadTask.IsCompleted)
                        continue; // whenMustFlush is completed, waitToReadTask is not
                }
            }

            bool canRead;
            if (waitToReadTask is not null) {
                canRead = await waitToReadTask.ConfigureAwait(false);
                waitToReadTask = null;
            }
            else
                canRead = await reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false);
            if (!canRead)
                break; // Reading is done

            while (reader.TryRead(out var message)) {
                try {
                    serialize(message, _writeBuffer);
                    message.CompleteWhenSerialized();
                }
                catch (Exception e) {
                    message.CompleteWhenSerialized(e);
                    continue;
                }

                if (_writeBuffer.WrittenCount >= _writeFrameSize) {
                    await lastFlushTask.ConfigureAwait(false);
                    lastFlushTask = FlushFrame();
                    whenMustFlush = null;
                }
            }
            if (whenMustFlush is null && _writeBuffer.WrittenCount > 0)
                whenMustFlush = frameDelayer.Invoke(_writeBuffer.WrittenCount);
        }

        // Final flush
        if (_writeBuffer.WrittenCount != 0) {
            await lastFlushTask.ConfigureAwait(false);
            lastFlushTask = FlushFrame();
        }
        // Await the last flush
        await lastFlushTask.ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Task FlushFrame()
    {
        // Swap _flushingBuffer and _writeBuffer
        (_flushingBuffer, _writeBuffer) = (_writeBuffer, _flushingBuffer);
        var frame = _flushingBuffer.WrittenMemory;
        _writeBuffer.Renew(Settings.MaxWriteBufferSize);

        _meters.OutgoingFrameSizeHistogram.Record(frame.Length);
        return WebSocket.SendAsync(frame, MessageType, endOfMessage: true, CancellationToken.None).AsTask();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SerializeBinary(RpcOutboundMessage message, ArrayPoolBuffer<byte> buffer)
    {
        _meters.OutgoingItemCounter.Add(1);
        var startOffset = buffer.WrittenCount;
        try {
            // Binary format: use 4-byte size prefix
            buffer.GetSpan(64);
            buffer.Advance(4);
            MessageSerializer.Write(buffer, message);
            var size = buffer.WrittenCount - startOffset;
            buffer.WrittenSpan.WriteUnchecked(size, startOffset);
        }
        catch (Exception e) {
            buffer.Position = startOffset;
            ErrorLog?.LogError(e, "Couldn't serialize the outbound message: {Message}", message);
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SerializeText(RpcOutboundMessage message, ArrayPoolBuffer<byte> buffer)
    {
        _meters.OutgoingItemCounter.Add(1);
        var startOffset = buffer.WrittenCount;
        try {
            // Text format: use LF+RS delimiter between messages (no size prefix)
            if (startOffset != 0) {
                var delimiterSpan = buffer.GetSpan(2);
                delimiterSpan[0] = LineFeed;
                delimiterSpan[1] = RecordSeparator;
                buffer.Advance(2);
            }
            MessageSerializer.Write(buffer, message);
        }
        catch (Exception e) {
            buffer.Position = startOffset;
            ErrorLog?.LogError(e, "Couldn't serialize the outbound message: {Message}", message);
            throw;
        }
    }

    private async IAsyncEnumerable<RpcInboundMessage> ReadAllImpl([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var minReadBufferSize = Settings.MinReadBufferSize;
        var maxReadBufferSize = Settings.MaxReadBufferSize;
        using var commonCts = cancellationToken.LinkWith(StopToken);
        using var commonTokenRegistration = commonCts.Token.Register(() => _ = DisposeAsync());

        // Start with a non-pooled buffer for initial reads
        var buffer = new ArrayPoolBuffer<byte>(minReadBufferSize, false);
        var tryDeserialize = IsTextSerializer
            ? (TryDeserializeMessageFunc)TryDeserializeText
            : TryDeserializeBinary;

        try {
            while (true) {
                var readMemory = buffer.GetMemory(minReadBufferSize);
                var arraySegment = new ArraySegment<byte>(buffer.Array, buffer.WrittenCount, readMemory.Length);
                WebSocketReceiveResult r;
                try {
                    r = await WebSocket.ReceiveAsync(arraySegment, CancellationToken.None).ConfigureAwait(false);
                }
                catch (WebSocketException e) when (e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
                    r = new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
                }
                if (r.MessageType == WebSocketMessageType.Close)
                    yield break;
                if (r.MessageType != MessageType)
                    throw Errors.InvalidWebSocketMessageType(r.MessageType, MessageType);

                buffer.Advance(r.Count);
                _meters.IncomingFrameSizeHistogram.Record(r.Count);
                if (!r.EndOfMessage)
                    continue; // Continue reading into the same buffer

                var array = buffer.Array;
                var totalLength = buffer.WrittenCount;
                var offset = 0;
                while (offset < totalLength) { // Zero-length frames are skipped here
                    var message = tryDeserialize(array, ref offset, totalLength);
                    if (message is not null)
                        yield return message;
                }
                // The code that uses frame's data (RpcInboundMessage.ArgumentData) is running synchronously,
                // so if we're here, the buffer can be reused.
                buffer.Renew(maxReadBufferSize);
            }
        }
        finally {
            buffer.Dispose();
            _ = DisposeAsync();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RpcInboundMessage? TryDeserializeBinary(byte[] array, ref int offset, int totalLength)
    {
        _meters.IncomingItemCounter.Add(1);
        int size = 0;
        bool isSizeValid = false;
        try {
            size = array.AsSpan(offset).ReadUnchecked<int>();
            isSizeValid = size > 0 && offset + size <= totalLength;
            if (!isSizeValid)
                throw Errors.InvalidItemSize();

            // Read message - ArgumentData is a projection into our buffer (zero-copy)
            var messageData = array.AsMemory(offset + sizeof(int), size - sizeof(int));
            var inboundMessage = MessageSerializer.Read(messageData, out int readSize);
            if (readSize != size - sizeof(int))
                throw Errors.InvalidItemSize();

            offset += size;
            return inboundMessage;
        }
        catch (Exception e) {
            var remaining = array.AsMemory(offset, totalLength - offset);
            ErrorLog?.LogError(e, "Couldn't deserialize: {Data}", new TextOrBytes(DataFormat.Bytes, remaining));
            offset = isSizeValid ? offset + size : totalLength;
            return null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RpcInboundMessage? TryDeserializeText(byte[] array, ref int offset, int totalLength)
    {
        _meters.IncomingItemCounter.Add(1);
        var remaining = array.AsSpan(offset, totalLength - offset);

        // Find Record Separator (RS) - messages are delimited by LF+RS
        var rsIndex = remaining.IndexOf(RecordSeparator);
        // Message length: up to RS (or end of buffer), minus trailing LF before RS
        var messageLength = rsIndex < 0
            ? remaining.Length
            : rsIndex; // RS position = message length (LF is at rsIndex-1, so message is [0..rsIndex-1] + LF)

        try {
            // Pass limited slice to serializer (like master does)
            var messageData = array.AsMemory(offset, messageLength);
            return MessageSerializer.Read(messageData, out _);
        }
        catch (Exception e) {
            var remainingMemory = array.AsMemory(offset, totalLength - offset);
            ErrorLog?.LogError(e, "Couldn't deserialize: {Data}", new TextOrBytes(DataFormat.Text, remainingMemory));
            return null;
        }
        finally {
            // Advance past message and RS delimiter (if present)
            offset = rsIndex < 0
                ? totalLength // Consumed entire buffer
                : offset + rsIndex + 1; // Skip past RS
        }
    }

    // This method should never throw
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
