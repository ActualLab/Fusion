using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using ActualLab.IO;
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

        public Func<FrameDelayer?>? FrameDelayerFactory { get; init; } = FrameDelayerFactories.None;

        public int WriteFrameSize { get; init; } = 12_000; // 8 x 1500 (min. MTU) minus some reserve
        public int MinWriteBufferSize { get; init; } = 24_000;
        public int MinReadBufferSize { get; init; } = 24_000;
        public int MaxPendingFrameCount { get; init; } = 8;
        public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(10);
    }

    private static readonly MeterSet StaticMeters = new();

    private readonly MeterSet _meters = StaticMeters;
    private readonly int _writeFrameSize;
    private readonly int _minReadBufferSize;
    private readonly AsyncTaskMethodBuilder _whenCompletedSource;
    private readonly Task _whenCompleted;
    private readonly Channel<ArrayOwner<byte>> _frameChannel;
    private readonly ChannelWriter<ArrayOwner<byte>> _frameWriter;
    private readonly ArrayPoolBuffer<byte> _writeBuffer;
    private readonly ConcurrentQueue<RpcOutboundMessage> _writeQueue = new();
    private int _writerCount;
    private readonly FrameDelayer? _frameDelayer;
    private int _scheduledFlushId;
    private int _flushIdGenerator;
    private volatile Task _whenLastWriteCompleted = Task.CompletedTask;
    private int _getAsyncEnumeratorCounter;

    public bool OwnsWebSocketOwner { get; set; } = true;
    public Options Settings { get; }
    public WebSocketOwner WebSocketOwner { get; }
    public WebSocket WebSocket { get; }
    public RpcPeer Peer { get; }
    public RpcByteMessageSerializerV4 MessageSerializer { get; }
    public ILogger? Log { get; }
    public ILogger? ErrorLog { get; }

    public override Task WhenCompleted => _whenCompleted;
    public override Task WhenClosed { get; }

    public WebSocketRpcTransport(
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
        MessageSerializer = new RpcByteMessageSerializerV4(peer);

        Log = webSocketOwner.Services.LogFor(GetType());
        ErrorLog = Log.IfEnabled(LogLevel.Error);

        _whenCompletedSource = AsyncTaskMethodBuilderExt.New();
        _whenCompleted = _whenCompletedSource.Task;

        _writeFrameSize = settings.WriteFrameSize;
        if (_writeFrameSize <= 0)
            throw new ArgumentOutOfRangeException($"{nameof(settings)}.{nameof(settings.WriteFrameSize)} must be positive.");

        _minReadBufferSize = settings.MinReadBufferSize;
        _writeBuffer = new ArrayPoolBuffer<byte>(settings.MinWriteBufferSize, mustClear: false);
        _frameDelayer = settings.FrameDelayerFactory?.Invoke();
        _frameChannel = Channel.CreateBounded<ArrayOwner<byte>>(
            new BoundedChannelOptions(settings.MaxPendingFrameCount) {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait,
            });
        _frameWriter = _frameChannel.Writer;

        using var __ = ExecutionContextExt.TrySuppressFlow();
        WhenClosed = Task.Run(async () => {
            Interlocked.Increment(ref _meters.ChannelCount);
            try {
                var whenFrameSenderCompleted = Task.Run(RunFrameSender, CancellationToken.None);
                await Task.WhenAny(_whenCompleted, whenFrameSenderCompleted).SilentAwait(false);
                StopTokenSource.Cancel(); // Stops frame sender
                TryComplete(); // Stops writes
                await whenFrameSenderCompleted.ConfigureAwait(false); // RunFrameSender never throws
                await _whenCompleted.SilentAwait(false); // Can fail, so we use SilentAwait here

                // Waiting to become write primary
                while (Interlocked.CompareExchange(ref _writerCount, 1, 0) != 0)
                    await Task.Yield();
                // We're the primary now, draining write queue
                while (_writeQueue.TryDequeue(out var message))
                    message.CompleteWhenSerialized(new ChannelClosedException());
                _writeBuffer.Dispose();
                Interlocked.Decrement(ref _writerCount);

                await CloseWebSocket(null).ConfigureAwait(false); // CloseWebSocket never throws
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

    // Lock-free write: uses atomic _writerCount to determine who serializes.
    // Write primary (count==1) serializes messages and may flush buffer.
    public override Task Write(RpcOutboundMessage message, CancellationToken cancellationToken = default)
    {
        if (_whenCompleted.IsCompleted)
            return Task.FromException(new ChannelClosedException());

        var count = Interlocked.Increment(ref _writerCount);
        if (count == 1) {
            var whenWriteCompleted = _whenLastWriteCompleted;
            return whenWriteCompleted.IsCompleted
                ? WriteAsPrimary(message)
                : WriteAsPrimary(message, whenWriteCompleted);
        }

        message.PrepareWhenSerialized();
        _writeQueue.Enqueue(message);
        // We might have enqueued right after the current primary drained the queue
        // and is about to release. If we drop the counter to 0, there may be no
        // subsequent writer to drain the queue, so we need a handoff.
        if (Interlocked.Decrement(ref _writerCount) == 0 && Interlocked.CompareExchange(ref _writerCount, 1, 0) == 0) {
            // We are the primary now, draining write queue
            var whenLastWriteCompleted = _whenLastWriteCompleted;
            _ = whenLastWriteCompleted.IsCompleted
                ? WriteAsPrimary(null)
                : WriteAsPrimary(null, whenLastWriteCompleted);
        }
        return message.WhenSerialized;
    }

    public override bool TryComplete(Exception? error = null)
    {
        if (!_whenCompletedSource.TrySetFromResult(new Result<Unit>(default, error)))
            return false;

        _frameWriter.TryComplete(error);
        return true;
    }

    public override IAsyncEnumerator<RpcInboundMessage> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => Interlocked.Increment(ref _getAsyncEnumeratorCounter) == 1
            ? ReadAllImpl(cancellationToken).GetAsyncEnumerator(cancellationToken)
            : throw ActualLab.Internal.Errors.AlreadyInvoked($"{GetType().GetName()}.GetAsyncEnumerator");

    // Private methods

    private async Task WriteAsPrimary(RpcOutboundMessage? message, Task whenReadyToWrite)
    {
        await whenReadyToWrite.ConfigureAwait(false);
        await WriteAsPrimary(message).ConfigureAwait(false);
    }

    private Task WriteAsPrimary(RpcOutboundMessage? message)
        => WriteAsPrimaryCore(message, mustFlush: false);

    private Task WriteAsPrimaryCore(RpcOutboundMessage? message, bool mustFlush)
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
            if (bufferSize <= 0) {
                Volatile.Write(ref _scheduledFlushId, 0);
                return Task.CompletedTask;
            }
            if (!mustFlush && bufferSize < _writeFrameSize) {
                // Below the max frame size: schedule a flush if it's not scheduled yet.
                // If there is no frame delayer, we schedule it as a yield-like continuation.
                if (Volatile.Read(ref _scheduledFlushId) == 0)
                    ScheduleFlush(bufferSize);
                return Task.CompletedTask;
            }

            Volatile.Write(ref _scheduledFlushId, 0);
            // Flushing write buffer
            var frame = writeBuffer.ToArrayOwnerAndReset(Settings.MinWriteBufferSize);
            if (_frameWriter.TryWrite(frame))
                return Task.CompletedTask;

            var whenWriteCompleted = _frameWriter.WriteAsync(frame).AsTask();
            _ = Interlocked.Exchange(ref _whenLastWriteCompleted, whenWriteCompleted);
            return whenWriteCompleted;
        }
        catch (Exception e) {
            TryComplete(e);
            return Task.FromException(e);
        }
        finally {
            Interlocked.Decrement(ref _writerCount);
        }
    }

    private void ScheduleFlush(int frameSize)
    {
        var flushId = Interlocked.Increment(ref _flushIdGenerator);
        if (Interlocked.CompareExchange(ref _scheduledFlushId, flushId, 0) != 0)
            return;

        var whenDelayCompleted = _frameDelayer?.Invoke(frameSize) ?? TaskExt.YieldDelay();
        _ = whenDelayCompleted.IsCompleted
            ? FlushScheduled(flushId)
            : whenDelayCompleted.ContinueWith(
                    _ => FlushScheduled(flushId),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default)
                .Unwrap();
    }

    private async Task FlushScheduled(int flushId)
    {
        while (Volatile.Read(ref _scheduledFlushId) == flushId) {
            if (Interlocked.CompareExchange(ref _writerCount, 1, 0) == 0)
                break;
            await Task.Yield();
        }
        if (Volatile.Read(ref _scheduledFlushId) != flushId)
            return;

        // We're the primary writer now: force a flush (and drain any queued messages first)
        await WriteAsPrimaryCore(null, mustFlush: true).ConfigureAwait(false);
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

    // This method should never throw
    private async Task RunFrameSender()
    {
        Exception? error = null;
        var frameReader = _frameChannel.Reader;
        try {
            while (true) {
                // Drain all ready frames from the channel first.
                while (frameReader.TryRead(out var frame)) {
                    await WebSocket
                        .SendAsync(frame.Memory, WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None)
                        .ConfigureAwait(false);
                    _meters.OutgoingFrameSizeHistogram.Record(frame.Length);
                    frame.Dispose();
                }

                // Nothing to send: wait for a channel frame.
                if (!await frameReader.WaitToReadAsync(StopToken).ConfigureAwait(false))
                    return;
            }
        }
        catch (Exception e) {
            // This method should never throw
            if (!e.IsCancellationOf(StopToken))
                error = e;
        }
        finally {
            TryComplete(error);
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

                var frame = readBuffer.ToArrayOwnerAndReset(_minReadBufferSize);
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
    private RpcInboundMessage? TryDeserialize(ArrayOwner<byte> frame, ref int offset, int totalLength)
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
