using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using ActualLab.Channels;
using ActualLab.IO;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Serialization;
using ActualLab.Rpc.WebSockets.Internal;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.WebSockets;

public sealed class WebSocketChannel<T> : Channel<T>, IChannelWithReadAllUnbuffered<T>
    where T : class
{
    public record Options
    {
        public static readonly Options Default = new();

        public int WriteFrameSize { get; init; } = 12_000; // 8 x 1500 (min. MTU) minus some reserve
        public int MinWriteBufferSize { get; init; } = 24_000; // Rented ~just once, so it can be large
        public int MinReadBufferSize { get; init; } = 24_000; // Rented ~just once, so it can be large
        public int RetainedBufferSize { get; init; } = 120_000; // Read buffer is released when it hits this size
        public int BufferRenewPeriod { get; init; } = 100; // Per flush/read cycle
        public RpcFrameDelayerFactory? FrameDelayerFactory { get; init; }
        public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(10);
        public IByteSerializer<T> Serializer { get; init; } = ActualLab.Serialization.ByteSerializer.Default.ToTyped<T>();
        public ChannelOptions ReadChannelOptions { get; init; } = new BoundedChannelOptions(240) {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = true,
        };
        public ChannelOptions WriteChannelOptions { get; init; } = new BoundedChannelOptions(240) {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = true,
        };
    }

    private static readonly MeterSet StaticMeters = new();
    private const int MinWriteSpanSize = 64;

    private volatile CancellationTokenSource? _stopCts;
    private readonly Channel<T>? _readChannel;
    private readonly Channel<T> _writeChannel;
    private readonly ArrayPoolBuffer<byte> _writeBuffer;
    private readonly int _writeFrameSize;
    private readonly int _retainedBufferSize;
    private readonly int _bufferRenewPeriod;
    private readonly MeterSet _meters = StaticMeters;
    private int _readBufferResetCounter;
    private int _writeBufferResetCounter;
    private Task? _lastFlushFrameTask;

    public bool OwnsWebSocketOwner { get; init; } = true;
    public bool UseReadAllUnbuffered => _readChannel is null;
    public Options Settings { get; }
    public WebSocketOwner WebSocketOwner { get; }
    public WebSocket WebSocket { get; }
    public readonly CancellationToken StopToken;
    public readonly ITextSerializer<T>? TextSerializer;
    public readonly IByteSerializer<T>? ByteSerializer;
    public readonly IProjectingByteSerializer<T>? ProjectingByteSerializer;
    public readonly DataFormat DataFormat;
    public readonly WebSocketMessageType MessageType;
    public readonly bool RequiresItemSize;
    public readonly ILogger? Log;
    public readonly ILogger? ErrorLog;
    public readonly Task WhenClosing; // Throws OperationCanceledException
    public readonly Task WhenReadCompleted;
    public readonly Task WhenWriteCompleted;
    public readonly Task WhenClosed;

    public WebSocketChannel(
        WebSocketOwner webSocketOwner,
        bool useReadAllUnbuffered,
        CancellationToken cancellationToken = default)
        : this(Options.Default, webSocketOwner, useReadAllUnbuffered, cancellationToken)
    { }

    public WebSocketChannel(
        Options settings,
        WebSocketOwner webSocketOwner,
        bool useReadAllUnbuffered,
        CancellationToken cancellationToken = default)
    {
        Settings = settings;
        WebSocketOwner = webSocketOwner;
        WebSocket = webSocketOwner.WebSocket;
        _stopCts = cancellationToken.CreateLinkedTokenSource();
        StopToken = _stopCts.Token;

        TextSerializer = settings.Serializer as ITextSerializer<T>; // ITextSerializer<T> is also IByteSerializer<T>
        ByteSerializer = TextSerializer is null ? settings.Serializer : null;
        ProjectingByteSerializer = ByteSerializer as IProjectingByteSerializer<T>;
        if (ProjectingByteSerializer is { AllowProjection: false })
            ProjectingByteSerializer = null;
        (DataFormat, MessageType) = TextSerializer is not null
            ? (DataFormat.Text, WebSocketMessageType.Text)
            : (DataFormat.Bytes, WebSocketMessageType.Binary);
        RequiresItemSize = ByteSerializer is IRequiresItemSize;

        Log = webSocketOwner.Services.LogFor(GetType());
        ErrorLog = Log.IfEnabled(LogLevel.Error);

        _writeFrameSize = settings.WriteFrameSize;
        if (_writeFrameSize <= 0)
            throw new ArgumentOutOfRangeException($"{nameof(settings)}.{nameof(settings.WriteFrameSize)} must be positive.");
        _retainedBufferSize = settings.RetainedBufferSize;
        _bufferRenewPeriod = settings.BufferRenewPeriod;
        _writeBuffer = new ArrayPoolBuffer<byte>(settings.MinWriteBufferSize, false);

        _readChannel = useReadAllUnbuffered ? null : ChannelExt.Create<T>(settings.ReadChannelOptions);
        _writeChannel = ChannelExt.Create<T>(settings.WriteChannelOptions);
        Reader = _readChannel?.Reader!;
        Writer = _writeChannel.Writer;

        using var _ = ExecutionContextExt.TrySuppressFlow();
        WhenClosing = TaskExt.NeverEnding(StopToken);
        WhenReadCompleted = Task.Run(() => RunReader(StopToken), default);
        WhenWriteCompleted = Task.Run(() => RunWriter(StopToken), default);
        WhenClosed = Task.Run(async () => {
            Interlocked.Increment(ref _meters.ChannelCount);
            try {
                await Task.WhenAny(WhenReadCompleted, WhenWriteCompleted, WhenClosing).SilentAwait(false);
                // We use CancellationToken.None SendAsync/ReceiveAsync calls,
                // so the first thing to do here is to close the WebSocket
                // to abort all ongoing WebSocket operations.
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

    public IAsyncEnumerable<T> ReadAllUnbuffered(CancellationToken cancellationToken = default)
        => ProjectingByteSerializer is not null
            ? ReadAllProjecting(cancellationToken)
            : ReadAll(cancellationToken);

    public Task Close()
    {
        var stopCts = Interlocked.Exchange(ref _stopCts, null);
        stopCts.CancelAndDisposeSilently();
        return WhenClosed;
    }

    // Private methods

    private async Task RunReader(CancellationToken cancellationToken)
    {
        var writer = _readChannel?.Writer;
        if (writer is null) {
            await TaskExt.NeverEnding(StopToken).SilentAwait(false);
            return;
        }

        try {
            var items = ProjectingByteSerializer is not null
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

    private async Task RunWriter(CancellationToken cancellationToken)
    {
        try {
            if (Settings.FrameDelayerFactory?.Invoke() is { } frameDelayer) {
                // There is a frame delayer -> we use more complex write logic
                await RunWriterWithFrameDelayer(frameDelayer, cancellationToken).ConfigureAwait(false);
                return;
            }

            // Simpler logic for no frame delayer case
            var reader = _writeChannel.Reader;
            Func<T, ArrayPoolBuffer<byte>, bool> trySerialize = DataFormat == DataFormat.Bytes
                ? (RequiresItemSize ? TrySerializeBytesWithItemSize : TrySerializeBytes)
                : TrySerializeText;

            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false)) {
                while (reader.TryRead(out var item)) {
                    if (trySerialize.Invoke(item, _writeBuffer) && _writeBuffer.WrittenCount >= _writeFrameSize)
                        await FlushFrame().ConfigureAwait(false);
                }
                // Final flush before await
                if (_writeBuffer.WrittenCount != 0)
                    await FlushFrame().ConfigureAwait(false);
            }
        }
        finally {
            _writeBuffer.Dispose();
            _ = Close();
        }
    }

    private async Task RunWriterWithFrameDelayer(RpcFrameDelayer frameDelayer, CancellationToken cancellationToken)
    {
        Task? whenMustFlush = null; // null = no flush required / nothing to flush
        Task<bool>? waitToReadTask = null;
        var reader = _writeChannel.Reader;
        Func<T, ArrayPoolBuffer<byte>, bool> trySerialize = DataFormat == DataFormat.Bytes
            ? (RequiresItemSize ? TrySerializeBytesWithItemSize : TrySerializeBytes)
            : TrySerializeText;

        while (true) {
            // When we are here, the sync read part is completed, so WaitToReadAsync will likely await.
            if (whenMustFlush is not null) {
                if (whenMustFlush.IsCompleted) {
                    // Flush is required right now.
                    // We aren't going to check WaitToReadAsync, coz most likely it's going to await.
                    if (_writeBuffer.WrittenCount != 0)
                        await FlushFrame().ConfigureAwait(false);
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
            // - whenMustFlush is null -> we only need to await for waitToReadTask or WaitToReadAsync
            // - both whenMustFlush and waitToReadTask are completed
            bool canRead;
            if (waitToReadTask is not null) {
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
                    await FlushFrame().ConfigureAwait(false);
                    // We just "crossed" _writeFrameSize boundary, so the flush we just made
                    // flushed everything except maybe the most recent item.
                    // We can safely "declare" that if any flush was expected before that moment,
                    // it just happened. As for the most recent item, see the next "if".
                    whenMustFlush = null;
                }
            }
            if (whenMustFlush is null && _writeBuffer.WrittenCount > 0) {
                // If we're here, the flush isn't "planned" yet + there is some data to flush.
                whenMustFlush = frameDelayer.Invoke(_writeBuffer.WrittenCount);
            }
        }
        // Final flush
        if (_writeBuffer.WrittenCount != 0)
            await FlushFrame().ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask FlushFrame()
    {
        var memory = _writeBuffer.WrittenMemory;
        if (memory.Length == 0) // We can't get here (see the calls to this method), but just in case...
            return;

        if (_lastFlushFrameTask is not null)
            await _lastFlushFrameTask.ConfigureAwait(false);
        _lastFlushFrameTask = WebSocket
            .SendAsync(memory, MessageType, endOfMessage: true, cancellationToken: default)
            .AsTask();
        _meters.OutgoingFrameSizeHistogram.Record(memory.Length);

        if (MustRenewBuffer(ref _writeBufferResetCounter))
            _writeBuffer.Renew(Settings.MinWriteBufferSize, _retainedBufferSize);
        else
            _writeBuffer.Reset();
    }

    private async IAsyncEnumerable<T> ReadAll([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var minReadBufferSize = Settings.MinReadBufferSize;
        var readBuffer = new ArrayPoolBuffer<byte>(minReadBufferSize, false);
        TryDeserializeFunc tryDeserialize = DataFormat == DataFormat.Bytes
            ? (RequiresItemSize ? TryDeserializeBytesWithItemSize : TryDeserializeBytes)
            : TryDeserializeText;

        using var linkedCts = cancellationToken.LinkWith(StopToken);
        var linkedToken = linkedCts.Token;
        using var linkedTokenRegistration = linkedToken.Register(() => _ = Close());
        try {
            while (true) {
                var readMemory = readBuffer.GetMemory(minReadBufferSize);
                var arraySegment = new ArraySegment<byte>(readBuffer.Array, readBuffer.WrittenCount, readMemory.Length);
                WebSocketReceiveResult r;
                try {
                    r = await WebSocket.ReceiveAsync(arraySegment, cancellationToken: default).ConfigureAwait(false);
                }
                catch (WebSocketException e) when (e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
                    // This is a normal closure in most of the cases,
                    // so we don't want to report it as an error
                    r = new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
                }
                if (r.MessageType == WebSocketMessageType.Close)
                    yield break;
                if (r.MessageType != MessageType)
                    throw Errors.InvalidWebSocketMessageType(r.MessageType, MessageType);

                readBuffer.Advance(r.Count);
                // _meters.IncomingFrameCounter.Add(1);
                _meters.IncomingFrameSizeHistogram.Record(r.Count);
                if (!r.EndOfMessage)
                    continue; // Continue reading into the same buffer

                var buffer = readBuffer.WrittenMemory;
                while (buffer.Length != 0)
                    if (tryDeserialize.Invoke(ref buffer, out var value))
                        yield return value;

                if (MustRenewBuffer(ref _readBufferResetCounter))
                    readBuffer.Renew(minReadBufferSize, _retainedBufferSize);
                else
                    readBuffer.Reset();
            }
        }
        finally {
            readBuffer.Dispose();
            _ = Close();
        }
    }

    private async IAsyncEnumerable<T> ReadAllProjecting([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var minReadBufferSize = Settings.MinReadBufferSize;
        var readBuffer = new ArrayPoolBuffer<byte>(minReadBufferSize, false);
        TryProjectingDeserializeFunc tryProjectingDeserialize = RequiresItemSize
            ? TryProjectingDeserializeBytesWithItemSize
            : TryProjectingDeserializeBytes;

        using var linkedCts = cancellationToken.LinkWith(StopToken);
        var linkedToken = linkedCts.Token;
        using var linkedTokenRegistration = linkedToken.Register(() => _ = Close());
        try {
            while (true) {
                var readMemory = readBuffer.GetMemory(minReadBufferSize);
                var arraySegment = new ArraySegment<byte>(readBuffer.Array, readBuffer.WrittenCount, readMemory.Length);
                WebSocketReceiveResult r;
                try {
                    r = await WebSocket.ReceiveAsync(arraySegment, cancellationToken: default).ConfigureAwait(false);
                }
                catch (WebSocketException e) when (e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
                    // This is a normal closure in most of the cases,
                    // so we don't want to report it as an error
                    r = new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
                }
                if (r.MessageType == WebSocketMessageType.Close)
                    yield break;
                if (r.MessageType != MessageType)
                    throw Errors.InvalidWebSocketMessageType(r.MessageType, MessageType);

                readBuffer.Advance(r.Count);
                // _meters.IncomingFrameCounter.Add(1);
                _meters.IncomingFrameSizeHistogram.Record(r.Count);
                if (!r.EndOfMessage)
                    continue;

                var buffer = readBuffer.WrittenMemory;
                var gotAnyProjection = false;
                while (buffer.Length != 0) {
                    if (tryProjectingDeserialize.Invoke(ref buffer, out var value, out var isProjection))
                        yield return value;

                    gotAnyProjection |= isProjection;
                }

                if (gotAnyProjection)
                    readBuffer = new ArrayPoolBuffer<byte>(minReadBufferSize, false);
                else if (MustRenewBuffer(ref _readBufferResetCounter))
                    readBuffer.Renew(minReadBufferSize, _retainedBufferSize);
                else
                    readBuffer.Reset();
            }
        }
        finally {
            readBuffer.Dispose();
            _ = Close();
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
            return; // ClientWebSocket throws an exception on closing a closed WebSocket - we don't want that

        try {
            await WebSocket.CloseAsync(status, message, default)
                .WaitAsync(Settings.CloseTimeout, CancellationToken.None)
                .SilentAwait(false);
        }
        catch {
            // Intended
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TrySerializeBytes(T value, ArrayPoolBuffer<byte> buffer)
    {
        _meters.OutgoingItemCounter.Add(1);
        var startOffset = buffer.WrittenCount;
        try {
            ByteSerializer!.Write(buffer, value);
            // Log?.LogInformation("Wrote: {Value}", value);
            // Log?.LogInformation("Data({Size}): {Data}",
            //     size - 4, new ByteString(buffer.WrittenMemory[(startOffset + 4)..].ToArray()));
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TrySerializeBytesWithItemSize(T value, ArrayPoolBuffer<byte> buffer)
    {
        _meters.OutgoingItemCounter.Add(1);
        var startOffset = buffer.WrittenCount;
        try {
            buffer.GetSpan(MinWriteSpanSize);
            buffer.Advance(4);
            ByteSerializer!.Write(buffer, value);
            var size = buffer.WrittenCount - startOffset;
            buffer.WrittenSpan.WriteUnchecked(size, startOffset);

            // Log?.LogInformation("Wrote: {Value}", value);
            // Log?.LogInformation("Data({Size}): {Data}",
            //     size - 4, new ByteString(buffer.WrittenMemory[(startOffset + 4)..].ToArray()));
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TrySerializeText(T value, ArrayPoolBuffer<byte> buffer)
    {
        _meters.OutgoingItemCounter.Add(1);
        var startOffset = buffer.WrittenCount;
        if (startOffset != 0) {
            // Write the delimiter
            var delimiterSpan = buffer.GetSpan(2);
            delimiterSpan[0] = WebSocketChannelImpl.LineFeed;
            delimiterSpan[1] = WebSocketChannelImpl.RecordSeparator;
            buffer.Advance(2);
        }
        try {
            TextSerializer!.Write(buffer, value);
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryDeserializeBytes(ref ReadOnlyMemory<byte> bytes, out T value)
    {
        _meters.IncomingItemCounter.Add(1);
        try {
            value = ByteSerializer!.Read(bytes, out int size);
            bytes = bytes[size..];
            // Log?.LogInformation("Read: {Value}", value);
            // Log?.LogInformation("Data({Size}): {Data}",  readSize, new ByteString(data.ToArray()));
            return true;
        }
        catch (Exception e) {
            ErrorLog?.LogError(e, "Couldn't deserialize: {Data}", new TextOrBytes(DataFormat.Bytes, bytes));
            bytes = default;
            value = default!;
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryProjectingDeserializeBytes(ref ReadOnlyMemory<byte> bytes, out T value, out bool isProjection)
    {
        _meters.IncomingItemCounter.Add(1);
        try {
            value = ProjectingByteSerializer!.Read(bytes, out int size, out isProjection);
            bytes = bytes[size..];
            // Log?.LogInformation("Read: {Value}", value);
            // Log?.LogInformation("Data({Size}): {Data}", readSize, new ByteString(data.ToArray()));
            return true;
        }
        catch (Exception e) {
            ErrorLog?.LogError(e, "Couldn't deserialize: {Data}", new TextOrBytes(DataFormat.Bytes, bytes));
            bytes = default;
            value = default!;
            isProjection = false;
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryDeserializeBytesWithItemSize(ref ReadOnlyMemory<byte> bytes, out T value)
    {
        _meters.IncomingItemCounter.Add(1);
        int size = 0;
        bool isSizeValid = false;
        try {
            size = bytes.Span.ReadUnchecked<int>();
            isSizeValid = size > 0 && size <= bytes.Length;
            if (!isSizeValid)
                throw Errors.InvalidItemSize();

            var data = bytes[sizeof(int)..size];
            value = ByteSerializer!.Read(data, out int readSize);
            if (readSize != size - 4)
                throw Errors.InvalidItemSize();

            // Log?.LogInformation("Read: {Value}", value);
            // Log?.LogInformation("Data({Size}): {Data}",  readSize, new ByteString(data.ToArray()));

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryProjectingDeserializeBytesWithItemSize(ref ReadOnlyMemory<byte> bytes, out T value, out bool isProjection)
    {
        _meters.IncomingItemCounter.Add(1);
        int size = 0;
        bool isSizeValid = false;
        try {
            size = bytes.Span.ReadUnchecked<int>();
            isSizeValid = size > 0 && size <= bytes.Length;
            if (!isSizeValid)
                throw Errors.InvalidItemSize();

            var data = bytes[sizeof(int)..size];
            value = ProjectingByteSerializer!.Read(data, out int readSize, out isProjection);
            if (readSize != size - 4)
                throw Errors.InvalidItemSize();

            // Log?.LogInformation("Read: {Value}", value);
            // Log?.LogInformation("Data({Size}): {Data}", readSize, new ByteString(data.ToArray()));

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryDeserializeText(ref ReadOnlyMemory<byte> bytes, out T value)
    {
        _meters.IncomingItemCounter.Add(1);
        var rsIndex = bytes.Span.IndexOf(WebSocketChannelImpl.RecordSeparator);
        var size = rsIndex < 0
            ? bytes.Length
            : rsIndex; // Full delimiter is (LF, RS), so we "trim" LF here
        try {
            if (size <= 0)
                throw Errors.InvalidItemSize();

            value = TextSerializer!.Read(bytes[..size], out _);
            return true;
        }
        catch (Exception e) {
            ErrorLog?.LogError(e, "Couldn't deserialize: {Data}", new TextOrBytes(DataFormat.Text, bytes));
            value = default!;
            return false;
        }
        finally {
            bytes = size < bytes.Length
                ? bytes[(size + 1)..]
                : default; // Empty memory
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
        // public readonly Counter<long> IncomingFrameCounter;
        // public readonly Counter<long> OutgoingFrameCounter;
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
            // IncomingFrameCounter = m.CreateCounter<long>($"{ms}.incoming.frame.count",
            //     null, "Number of frames received via WebSocketChannel.");
            // OutgoingFrameCounter = m.CreateCounter<long>($"{ms}.outgoing.frame.count",
            //     null, "Number of frames sent via WebSocketChannel.");
            IncomingFrameSizeHistogram = m.CreateHistogram<int>($"{ms}.incoming.frame.size",
                "By", "WebSocketChannel's incoming frame size in bytes.");
            OutgoingFrameSizeHistogram = m.CreateHistogram<int>($"{ms}.outgoing.frame.size",
                "By", "WebSocketChannel's outgoing frame size in bytes.");
        }
    }

    private delegate bool TryDeserializeFunc(ref ReadOnlyMemory<byte> data, out T value);
    private delegate bool TryProjectingDeserializeFunc(ref ReadOnlyMemory<byte> bytes, out T value, out bool isProjection);
}
