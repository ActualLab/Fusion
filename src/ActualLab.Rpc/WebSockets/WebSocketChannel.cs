using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using ActualLab.IO;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Diagnostics;
using Errors = ActualLab.Rpc.Internal.Errors;
using UnreferencedCode = ActualLab.Internal.UnreferencedCode;

namespace ActualLab.Rpc.WebSockets;

public sealed class WebSocketChannel<T> : Channel<T>
    where T : class
{
    public record Options
    {
        public static readonly Options Default = new();

        public int WriteFrameSize { get; init; } = 1450 * 3; // 1500 is the default MTU
        public int MinWriteBufferSize { get; init; } = 16_384; // Rented ~just once, so it can be large
        public int MinReadBufferSize { get; init; } = 16_384; // Rented ~just once, so it can be large
        public int RetainedBufferSize { get; init; } = 65_536; // Read buffer is released when it hits this size
        public int BufferResetPeriod { get; init; } = 64;
        public int MaxItemSize { get; init; } = 130_000_000; // 130 MB;
        public RpcFrameDelayerFactory? FrameDelayerFactory { get; init; }
        public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(10);
        public IByteSerializer<T> Serializer { get; init; } = ActualLab.Serialization.ByteSerializer.Default.ToTyped<T>();
        public BoundedChannelOptions ReadChannelOptions { get; init; } = new(128) {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = true,
        };
        public BoundedChannelOptions WriteChannelOptions { get; init; } = new(128) {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = true,
        };
    }

    private static readonly MeterSet StaticMeters = new();
    private const int MinMessageSize = 32;

    private volatile CancellationTokenSource? _stopCts;
    private readonly Channel<T> _readChannel;
    private readonly Channel<T> _writeChannel;
    private readonly ArrayPoolBuffer<byte> _writeBuffer;
    private readonly int _writeFrameSize;
    private readonly int _retainedBufferSize;
    private readonly int _bufferResetPeriod;
    private readonly int _maxItemSize;
    private readonly MeterSet _meters = StaticMeters;
    private int _readBufferResetCounter;
    private int _writeBufferResetCounter;

    public bool OwnsWebSocketOwner { get; init; } = true;
    public Options Settings { get; }
    public WebSocketOwner WebSocketOwner { get; }
    public WebSocket WebSocket { get; }
    public readonly CancellationToken StopToken;
    public readonly ITextSerializer<T>? TextSerializer;
    public readonly IByteSerializer<T>? ByteSerializer;
    public readonly IProjectingByteSerializer<T>? ProjectingByteSerializer;
    public readonly DataFormat DataFormat;
    public readonly WebSocketMessageType MessageType;
    public readonly ILogger? Log;
    public readonly ILogger? ErrorLog;
    public readonly Task WhenReadCompleted;
    public readonly Task WhenWriteCompleted;
    public readonly Task WhenClosed;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public WebSocketChannel(
        WebSocketOwner webSocketOwner,
        CancellationToken cancellationToken = default)
        : this(Options.Default, webSocketOwner, cancellationToken)
    { }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public WebSocketChannel(
        Options settings,
        WebSocketOwner webSocketOwner,
        CancellationToken cancellationToken = default)
    {
        Settings = settings;
        WebSocketOwner = webSocketOwner;
        WebSocket = webSocketOwner.WebSocket;
        _stopCts = cancellationToken.CreateLinkedTokenSource();
        StopToken = _stopCts.Token;

        TextSerializer = settings.Serializer as ITextSerializer<T>; // ITextSerializer<T> is also IByteSerializer<T>
        ByteSerializer = TextSerializer == null ? settings.Serializer : null;
        ProjectingByteSerializer = ByteSerializer as IProjectingByteSerializer<T>;
        if (ProjectingByteSerializer is { AllowProjection: false })
            ProjectingByteSerializer = null;
        (DataFormat, MessageType) = TextSerializer != null
            ? (DataFormat.Text, WebSocketMessageType.Text)
            : (DataFormat.Bytes, WebSocketMessageType.Binary);

        Log = webSocketOwner.Services.LogFor(GetType());
        ErrorLog = Log.IfEnabled(LogLevel.Error);

        _writeFrameSize = settings.WriteFrameSize;
        if (_writeFrameSize <= 0)
            throw new ArgumentOutOfRangeException($"{nameof(settings)}.{nameof(settings.WriteFrameSize)} must be positive.");
        _retainedBufferSize = settings.RetainedBufferSize;
        _bufferResetPeriod = settings.BufferResetPeriod;
        _maxItemSize = settings.MaxItemSize;
        _writeBuffer = new ArrayPoolBuffer<byte>(settings.MinWriteBufferSize);

        _readChannel = Channel.CreateBounded<T>(settings.ReadChannelOptions);
        _writeChannel = Channel.CreateBounded<T>(settings.WriteChannelOptions);
        Reader = _readChannel.Reader;
        Writer = _writeChannel.Writer;

        using var _ = ExecutionContextExt.TrySuppressFlow();
        WhenReadCompleted = Task.Run(() => RunReader(StopToken), default);
        WhenWriteCompleted = Task.Run(() => RunWriter(StopToken), default);
        WhenClosed = Task.Run(async () => {
            Interlocked.Increment(ref _meters.ChannelCount);
            try {
                var completedTask = await Task.WhenAny(WhenReadCompleted, WhenWriteCompleted).ConfigureAwait(false);
                if (completedTask != WhenWriteCompleted)
                    await WhenWriteCompleted.SilentAwait(false);
                else
                    await WhenReadCompleted.SilentAwait(false);

                try {
                    await completedTask.ConfigureAwait(false);
                }
                catch (Exception error) {
                    await CloseWebSocket(error).ConfigureAwait(false);
                    throw;
                }

                await CloseWebSocket(null).ConfigureAwait(false);
            }
            finally {
                Interlocked.Decrement(ref _meters.ChannelCount);
            }
        }, default);
    }

    public async ValueTask Close()
    {
        var stopCts = Interlocked.Exchange(ref _stopCts, null);
        if (stopCts == null)
            return;

        stopCts.CancelAndDisposeSilently();
        await WhenClosed.SilentAwait(false);
        if (OwnsWebSocketOwner)
            await WebSocketOwner.DisposeAsync().ConfigureAwait(false);
        _writeBuffer.Dispose();
    }

    // Private methods

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private async Task RunReader(CancellationToken cancellationToken)
    {
        var writer = _readChannel.Writer;
        try {
            var items = ProjectingByteSerializer != null
                ? ReadAllProjecting(cancellationToken)
                : ReadAll(cancellationToken);
            // ReSharper disable once UseCancellationTokenForIAsyncEnumerable
            await foreach (var item in items.ConfigureAwait(false)) {
                while (!writer.TryWrite(item))
                    if (!await writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false)) {
                        // This is a normal closure in most of the cases,
                        // so we don't want to report it as an error
                        return;
                    }
            }
        }
        catch (WebSocketException e) when (e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
            // This is a normal closure in most of the cases,
            // so we don't want to report it as an error
        }
        catch (Exception e) {
            writer.TryComplete(e);
        }
        finally {
            writer.TryComplete(); // We do this no matter what
            _ = Close();
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private async Task RunWriter(CancellationToken cancellationToken)
    {
        try {
            var reader = _writeChannel.Reader;
            if (DataFormat == DataFormat.Bytes) {
                // Binary -> we build frames
                if (Settings.FrameDelayerFactory?.Invoke() is { } frameDelayer) {
                    // There is a write delay -> we use more complex write logic
                    await RunWriterWithFrameDelayer(reader, frameDelayer, cancellationToken).ConfigureAwait(false);
                    return;
                }

                // Simpler logic for no write delay case
                while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false)) {
                    while (reader.TryRead(out var item)) {
                        if (TrySerializeBytes(item, _writeBuffer) && _writeBuffer.WrittenCount >= _writeFrameSize)
                            await FlushWriteBuffer(cancellationToken).ConfigureAwait(false);
                    }
                    if (_writeBuffer.WrittenCount != 0)
                        await FlushWriteBuffer(cancellationToken).ConfigureAwait(false);
                }
            }
            else {
                // Text -> no frames
                while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false)) {
                    while (reader.TryRead(out var item)) {
                        if (TrySerializeText(item, _writeBuffer))
                            await FlushWriteBuffer(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        finally {
            _ = Close();
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private async Task RunWriterWithFrameDelayer(
        ChannelReader<T> reader,
        RpcFrameDelayer frameDelayer,
        CancellationToken cancellationToken)
    {
        Task? whenMustFlush = null; // null = no flush required / nothing to flush
        Task<bool>? waitToReadTask = null;
        Func<T, ArrayPoolBuffer<byte>, bool> trySerialize = DataFormat == DataFormat.Bytes
            ? TrySerializeBytes
            : TrySerializeText;
        while (true) {
            // When we are here, the sync read part is completed, so WaitToReadAsync will likely await.
            if (whenMustFlush != null) {
                if (whenMustFlush.IsCompleted) {
                    // Flush is required right now.
                    // We aren't going to check WaitToReadAsync, coz most likely it's going to await.
                    if (_writeBuffer.WrittenCount != 0)
                        await FlushWriteBuffer(cancellationToken).ConfigureAwait(false);
                    whenMustFlush = null;
                }
                else {
                    // Flush is pending.
                    // We must await for either it or WaitToReadAsync - what comes first.
                    waitToReadTask ??= reader.WaitToReadAsync(cancellationToken).AsTask();
                    await Task.WhenAny(whenMustFlush, waitToReadTask).ConfigureAwait(false);
                    if (!waitToReadTask.IsCompleted)
                        continue; // whenMustFlush is completed, waitToReadTask is not
                }
            }

            // If we're here, it's either:
            // - whenMustFlush == null -> we only need to await for waitToReadTask or WaitToReadAsync
            // - both whenMustFlush and waitToReadTask are completed
            bool canRead;
            if (waitToReadTask != null) {
                canRead = await waitToReadTask.ConfigureAwait(false);
                waitToReadTask = null;
            }
            else
                canRead = await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
            if (!canRead)
                break; // Reading is done

            while (reader.TryRead(out var item)) {
                if (!trySerialize.Invoke(item, _writeBuffer))
                    continue; // Nothing is written

                if (_writeBuffer.WrittenCount >= _writeFrameSize) {
                    await FlushWriteBuffer(cancellationToken).ConfigureAwait(false);
                    // We just "crossed" _writeFrameSize boundary, so the flush we just made
                    // flushed everything except maybe the most recent item.
                    // We can safely "declare" that if any flush was expected before that moment,
                    // it just happened. As for the most recent item, see the next "if".
                    whenMustFlush = null;
                }
            }
            if (whenMustFlush == null && _writeBuffer.WrittenCount > 0) {
                // If we're here, the write flush isn't "planned" yet + there is some data to flush.
                whenMustFlush = frameDelayer.Invoke(_writeBuffer.WrittenCount);
            }
        }
        // Final write flush
        await FlushWriteBuffer(cancellationToken).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask FlushWriteBuffer(CancellationToken cancellationToken)
    {
        var message = _writeBuffer.WrittenMemory;
        if (message.Length == 0)
            return;

        await WebSocket
            .SendAsync(message, MessageType, true, cancellationToken)
            .ConfigureAwait(false);
        _meters.OutgoingFrameCounter.Add(1);
        _meters.OutgoingFrameSizeHistogram.Record(message.Length);

        if (MustReset(ref _writeBufferResetCounter))
            _writeBuffer.Reset(Settings.MinWriteBufferSize, _retainedBufferSize);
        else
            _writeBuffer.Reset();
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private async IAsyncEnumerable<T> ReadAll([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var minReadBufferSize = Settings.MinReadBufferSize;
        var readBuffer = new ArrayPoolBuffer<byte>(minReadBufferSize);
        try {
            while (true) {
                T value;
                var readMemory = readBuffer.GetMemory(minReadBufferSize);
                var arraySegment = new ArraySegment<byte>(readBuffer.Array, readBuffer.WrittenCount, readMemory.Length);
                var r = await WebSocket.ReceiveAsync(arraySegment, cancellationToken).ConfigureAwait(false);
                if (r.MessageType == WebSocketMessageType.Close)
                    yield break;
                if (r.MessageType != MessageType)
                    throw Errors.InvalidWebSocketMessageType(r.MessageType, MessageType);

                readBuffer.Advance(r.Count);
                _meters.IncomingFrameCounter.Add(1);
                _meters.IncomingFrameSizeHistogram.Record(r.Count);
                if (!r.EndOfMessage)
                    continue;

                var buffer = readBuffer.WrittenMemory;
                if (DataFormat == DataFormat.Bytes) {
                    while (buffer.Length != 0)
                        if (TryDeserializeBytes(ref buffer, out value))
                            yield return value;
                }
                else {
                    if (TryDeserializeText(buffer, out value))
                        yield return value;
                }

                if (MustReset(ref _readBufferResetCounter))
                    readBuffer.Reset(minReadBufferSize, _retainedBufferSize);
                else
                    readBuffer.Reset();
            }
        }
        finally {
            readBuffer.Dispose();
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private async IAsyncEnumerable<T> ReadAllProjecting([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var minReadBufferSize = Settings.MinReadBufferSize;
        var readBuffer = new ArrayPoolBuffer<byte>(minReadBufferSize);
        try {
            while (true) {
                var readMemory = readBuffer.GetMemory(minReadBufferSize);
                var arraySegment = new ArraySegment<byte>(readBuffer.Array, readBuffer.WrittenCount, readMemory.Length);
                var r = await WebSocket.ReceiveAsync(arraySegment, cancellationToken).ConfigureAwait(false);
                if (r.MessageType == WebSocketMessageType.Close)
                    yield break;
                if (r.MessageType != MessageType)
                    throw Errors.InvalidWebSocketMessageType(r.MessageType, MessageType);

                readBuffer.Advance(r.Count);
                _meters.IncomingFrameCounter.Add(1);
                _meters.IncomingFrameSizeHistogram.Record(r.Count);
                if (!r.EndOfMessage)
                    continue;

                var buffer = readBuffer.WrittenMemory;
                var gotProjection = false;
                while (buffer.Length != 0) {
                    if (TryProjectingDeserializeBytes(ref buffer, out var value, out var isProjection))
                        yield return value;

                    gotProjection |= isProjection;
                }

                if (gotProjection)
                    readBuffer = new ArrayPoolBuffer<byte>(minReadBufferSize);
                else if (MustReset(ref _readBufferResetCounter))
                    readBuffer.Reset(minReadBufferSize, _retainedBufferSize);
                else
                    readBuffer.Reset();
            }
        }
        finally {
            readBuffer.Dispose();
        }
    }

    private async Task CloseWebSocket(Exception? error)
    {
        if (error is OperationCanceledException)
            error = null;

        var status = WebSocketCloseStatus.NormalClosure;
        var message = "Ok.";
        if (error != null) {
            status = WebSocketCloseStatus.InternalServerError;
            message = "Internal Server Error.";
            ErrorLog?.LogError(error, "WebSocket is closing after an error");
        }

        try {
            await WebSocket.CloseAsync(status, message, default)
                .WaitAsync(Settings.CloseTimeout, CancellationToken.None)
                .SilentAwait(false);
        }
        catch {
            // Intended
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TrySerializeBytes(T value, ArrayPoolBuffer<byte> buffer)
    {
        _meters.OutgoingItemCounter.Add(1);
        var startOffset = buffer.WrittenCount;
        try {
            buffer.GetSpan(MinMessageSize);
            buffer.Advance(4);
            ByteSerializer!.Write(buffer, value);
            var size = buffer.WrittenCount - startOffset;
            buffer.WrittenSpan.WriteUnchecked(startOffset, size);
            if (size > _maxItemSize)
                throw Errors.ItemSizeExceedsTheLimit();

            // Log?.LogInformation("Wrote: {Value}", value);
            // Log?.LogInformation("Data({Size}): {Data}",
            //     size - 4, new Base64Encoded(buffer.WrittenMemory[(startOffset + 4)..].ToArray()).Encode());
            return true;
        }
        catch (Exception e) {
            buffer.Position = startOffset;
            ErrorLog?.LogError(e,
                "Couldn't serialize the value of type '{Type}'",
                value?.GetType().FullName ?? "null");
            return false;
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TrySerializeText(T value, ArrayPoolBuffer<byte> buffer)
    {
        _meters.OutgoingItemCounter.Add(1);
        var startOffset = buffer.WrittenCount;
        try {
            TextSerializer!.Write(buffer, value);
            var size = buffer.WrittenCount - startOffset;
            if (size > _maxItemSize)
                throw Errors.ItemSizeExceedsTheLimit();
            return true;
        }
        catch (Exception e) {
            buffer.Position = startOffset;
            ErrorLog?.LogError(e,
                "Couldn't serialize the value of type '{Type}'",
                value?.GetType().FullName ?? "null");
            return false;
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryDeserializeBytes(ref ReadOnlyMemory<byte> bytes, out T value)
    {
        _meters.IncomingItemCounter.Add(1);
        int size = 0;
        bool isSizeValid = false;
        try {
            size = bytes.Span.ReadUnchecked<int>();
            isSizeValid = size > 0 && size <= bytes.Length;
            if (!isSizeValid)
                throw Errors.InvalidItemSize();
            if (size > _maxItemSize)
                throw Errors.ItemSizeExceedsTheLimit();

            var data = bytes[sizeof(int)..size];
            value = ByteSerializer!.Read(data, out int readSize);
            if (readSize != size - 4)
                throw Errors.InvalidItemSize();

            // Log?.LogInformation("Read: {Value}", value);
            // Log?.LogInformation("Data({Size}): {Data}",
            //     readSize, new Base64Encoded(data.ToArray()).Encode());

            bytes = bytes[size..];
            return true;
        }
        catch (Exception e) {
            ErrorLog?.LogError(e, "Couldn't deserialize: {Data}", new TextOrBytes(DataFormat.Bytes, bytes));
            value = default!;
            bytes = isSizeValid ? bytes[size..] : default;
            return false;
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryProjectingDeserializeBytes(ref ReadOnlyMemory<byte> bytes, out T value, out bool isProjection)
    {
        _meters.IncomingItemCounter.Add(1);
        int size = 0;
        bool isSizeValid = false;
        try {
            size = bytes.Span.ReadUnchecked<int>();
            isSizeValid = size > 0 && size <= bytes.Length;
            if (!isSizeValid)
                throw Errors.InvalidItemSize();
            if (size > _maxItemSize)
                throw Errors.ItemSizeExceedsTheLimit();

            var data = bytes[sizeof(int)..size];
            value = ProjectingByteSerializer!.Read(data, out int readSize, out isProjection);
            if (readSize != size - 4)
                throw Errors.InvalidItemSize();

            // Log?.LogInformation("Read: {Value}", value);
            // Log?.LogInformation("Data({Size}): {Data}",
            //     readSize, new Base64Encoded(data.ToArray()).Encode());

            bytes = bytes[size..];
            return true;
        }
        catch (Exception e) {
            ErrorLog?.LogError(e, "Couldn't deserialize: {Data}", new TextOrBytes(DataFormat.Bytes, bytes));
            value = default!;
            bytes = isSizeValid ? bytes[size..] : default;
            isProjection = false;
            return false;
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryDeserializeText(ReadOnlyMemory<byte> bytes, out T value)
    {
        _meters.IncomingItemCounter.Add(1);
        try {
            if (bytes.Length > _maxItemSize)
                throw Errors.ItemSizeExceedsTheLimit();

            value = TextSerializer!.Read(bytes);
            return true;
        }
        catch (Exception e) {
            ErrorLog?.LogError(e, "Couldn't deserialize: {Data}", new TextOrBytes(DataFormat.Text, bytes));
            value = default!;
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MustReset(ref int counter)
    {
        if (++counter < _bufferResetPeriod)
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
        public readonly Counter<long> IncomingFrameCounter;
        public readonly Counter<long> OutgoingFrameCounter;
        public readonly Histogram<int> IncomingFrameSizeHistogram;
        public readonly Histogram<int> OutgoingFrameSizeHistogram;
        public long ChannelCount;

        public MeterSet()
        {
            var m = RpcInstruments.Meter;
            var ms = $"rpc.ws.{typeof(T).GetName()}-channel";
            ChannelCounter = m.CreateObservableCounter($"{ms}.count",
                () => Interlocked.Read(ref ChannelCount),
                null, "Number of WebSocketChannel instances.");
            IncomingItemCounter = m.CreateCounter<long>($"{ms}.incoming.item.count",
                null, "Number of items received via WebSocketChannel.");
            OutgoingItemCounter = m.CreateCounter<long>($"{ms}.outgoing.item.count",
                null, "Number of items sent via WebSocketChannel.");
            IncomingFrameCounter = m.CreateCounter<long>($"{ms}.incoming.frame.count",
                null, "Number of frames received via WebSocketChannel.");
            OutgoingFrameCounter = m.CreateCounter<long>($"{ms}.outgoing.frame.count",
                null, "Number of frames sent via WebSocketChannel.");
            IncomingFrameSizeHistogram = m.CreateHistogram<int>($"{ms}.incoming.frame.size",
                "By", "WebSocketChannel's incoming frame size in bytes.");
            OutgoingFrameSizeHistogram = m.CreateHistogram<int>($"{ms}.outgoing.frame.size",
                "By", "WebSocketChannel's outgoing frame size in bytes.");
        }
    }
}
