using System.Buffers;
using System.Diagnostics.Metrics;
using System.IO.Pipelines;
using ActualLab.Channels;
using ActualLab.Concurrency;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Serialization;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// An <see cref="RpcTransport"/> implementation that sends and receives RPC messages over a
/// <see cref="System.IO.Pipelines.PipeReader"/> / <see cref="System.IO.Pipelines.PipeWriter"/> pair. Unlike a WebSocket, a pipe is a raw
/// byte stream, so each frame (a batch of messages) is prefixed with its 4-byte little-endian length.
/// </summary>
public sealed class RpcPipeTransport : RpcTransport
{
    /// <summary>
    /// Configuration options for <see cref="RpcPipeTransport"/>.
    /// </summary>
    public record Options
    {
        public static readonly Options Default = new();

        public Func<RpcFrameDelayer?>? FrameDelayerFactory { get; init; } = RpcFrameDelayerFactories.None;

        public int FrameSize { get; init; } = 12_000; // 8 x 1500 (min. MTU minus some reserve)
        public int BufferSize { get; init; } = 16_000;
        public int MaxBufferSize { get; init; } = 256_000;
        public int MaxFrameSize { get; init; } = 16_000_000; // Inbound frame size guard against corrupt streams

        // Use of UnboundedChannelOptions is totally fine here: if the message is enqueued
        public ChannelOptions WriteChannelOptions { get; init; } = new UnboundedChannelOptions() {
            SingleReader = true, // Must be true
            SingleWriter = false, // Must be false
            AllowSynchronousContinuations = false, // Must be false, setting it to true will kill the throughput!
        };
    }

    private static readonly MeterSet StaticMeters = new();

    private readonly MeterSet _meters = StaticMeters;
    private readonly int _frameSize;
    private readonly int _maxBufferSize;
    private readonly Channel<RpcOutboundMessage> _writeChannel;
    private readonly ChannelWriter<RpcOutboundMessage> _writeChannelWriter;
    private readonly AsyncTaskMethodBuilder _whenCompletedSource;
    private readonly Task _whenCompleted;
    private readonly RpcFrameDelayer? _frameDelayer;
    private readonly RpcFrameCodec _codec;
    private ArrayPoolBuffer<byte> _writeBuffer;
    private ArrayPoolBuffer<byte> _flushingBuffer;
    private int _getAsyncEnumeratorCounter;

    public Options Settings { get; }
    public PipeReader PipeReader { get; }
    public PipeWriter PipeWriter { get; }
    public RpcMessageSerializer MessageSerializer { get; }
    // Owner is disposed (if it's IAsyncDisposable / IDisposable) once the transport is closed.
    public object? Owner { get; init; }
    public bool OwnsOwner { get; init; } = true;
    public ILogger? Log { get; }
    public ILogger? ErrorLog { get; }

    public override Task WhenCompleted => _whenCompleted;
    public Task WhenClosed { get; }

    public RpcPipeTransport(
        Options settings,
        RpcPeer peer,
        PipeReader pipeReader,
        PipeWriter pipeWriter,
        CancellationTokenSource? stopTokenSource = null)
        : base(peer, stopTokenSource)
    {
        Settings = settings;
        PipeReader = pipeReader;
        PipeWriter = pipeWriter;
        MessageSerializer = peer.MessageSerializer;

        Log = peer.Hub.Services.LogFor(GetType());
        ErrorLog = Log.IfEnabled(LogLevel.Error);

        _whenCompletedSource = AsyncTaskMethodBuilderExt.New();
        _whenCompleted = _whenCompletedSource.Task;

        _frameSize = settings.FrameSize;
        if (_frameSize <= 0)
            throw new ArgumentOutOfRangeException($"{nameof(settings)}.{nameof(settings.FrameSize)} must be positive.");
        _maxBufferSize = settings.MaxBufferSize;

        _frameDelayer = settings.FrameDelayerFactory?.Invoke();
        _codec = new RpcFrameCodec(
            MessageSerializer, _meters.IncomingItemCounter, _meters.OutgoingItemCounter, ErrorLog);
        _writeBuffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, Settings.BufferSize, mustClear: false);
        _flushingBuffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, Settings.BufferSize, mustClear: false);

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
                StopTokenSource.CancelSilently(); // Stops writer loop (and reader loop)
                TryComplete(); // Stops writes
                await whenWriterCompleted.ConfigureAwait(false); // RunWriter never throws
                await _whenCompleted.SilentAwait(false); // Can fail, so we use SilentAwait here

                // Drain remaining pending messages (if any) - RunWriter is stopped at that point
                while (_writeChannel.Reader.TryRead(out var message))
                    CompleteSend(message, new ChannelClosedException());

                ClosePipe(null); // ClosePipe never throws

                // It's safer to dispose the buffers here rather than in 'finally',
                // coz if something fails and they're somehow still used,
                // we simply won't return them back to the pool, so GC will take care of them.
                _flushingBuffer.Dispose();
                _writeBuffer.Dispose();
            }
            catch (Exception e) {
                Log.LogError(e, "Error in RpcPipeTransport.WhenClosed, this should never happen");
            }
            finally {
                Interlocked.Decrement(ref _meters.ChannelCount);
            }
        }, default);
    }

    protected override async Task DisposeAsyncCore()
    {
        await WhenClosed.ConfigureAwait(false);
        if (!OwnsOwner)
            return;

        switch (Owner) {
        case IAsyncDisposable ad:
            await ad.DisposeAsync().ConfigureAwait(false);
            break;
        case IDisposable d:
            d.Dispose();
            break;
        }
    }

    public override void Send(RpcOutboundMessage message, CancellationToken cancellationToken = default)
    {
        // Fast path: since _writeChannel is typically an UnboundedChannel,
        // TryWrite always completes successfully while the channel is operational.
        if (_writeChannelWriter.TryWrite(message))
            return;

        // Slow path
        _ = _writeChannelWriter.WriteAsync(message, cancellationToken);
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
            var serialize = _codec.Serialize;
            while (await reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false)) {
                while (reader.TryRead(out var message)) {
                    try {
                        serialize(message, _writeBuffer);
                        CompleteSend(message);
                    }
                    catch (Exception e) {
                        CompleteSend(message, e);
                    }
                    if (_writeBuffer.WrittenCount >= _frameSize) {
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
        var serialize = _codec.Serialize;

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
                    CompleteSend(message);
                }
                catch (Exception e) {
                    CompleteSend(message, e);
                    continue;
                }

                if (_writeBuffer.WrittenCount >= _frameSize) {
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
        _writeBuffer.Renew(_maxBufferSize);

        _meters.OutgoingFrameSizeHistogram.Record(frame.Length);
        return WriteFrame(frame);
    }

    private async Task WriteFrame(ReadOnlyMemory<byte> frame)
    {
        var writer = PipeWriter;
        var totalLength = sizeof(int) + frame.Length;
        var span = writer.GetSpan(totalLength);
        RpcByteMessageSerializerV5.WriteLittleEndian(span, frame.Length);
        frame.Span.CopyTo(span.Slice(sizeof(int)));
        writer.Advance(totalLength);
        var flushResult = await writer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        if (flushResult.IsCompleted || flushResult.IsCanceled)
            throw new ChannelClosedException();
    }

    private async IAsyncEnumerable<RpcInboundMessage> ReadAllImpl([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var commonCts = cancellationToken.LinkWith(StopToken);
        var reader = PipeReader;
        using var ctr = commonCts.Token.Register(
            static state => ((PipeReader)state!).CancelPendingRead(), reader);

        var tryDeserialize = _codec.TryDeserialize;
        var frameBuffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, Settings.BufferSize, mustClear: false);
        var frameLength = -1; // -1 = length header not read yet; >= 0 = bytes still being accumulated into frameBuffer
        try {
            while (true) {
                ReadResult result = default;
                var isDone = false;
                try {
                    result = await reader.ReadAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception e) {
                    Log?.LogWarning(e, "PipeReader.ReadAsync failed");
                    isDone = true;
                }
                if (isDone || result.IsCanceled)
                    yield break;

                var buffer = result.Buffer;
                var consumed = buffer.Start;

                // Phase 1: read the 4-byte frame length header (only the first time per frame)
                if (frameLength < 0) {
                    if (buffer.Length < sizeof(int)) {
                        if (result.IsCompleted)
                            yield break;
                        reader.AdvanceTo(buffer.Start, buffer.End); // need more data
                        continue;
                    }
                    frameLength = ReadFrameLength(buffer);
                    if (frameLength <= 0 || frameLength > Settings.MaxFrameSize)
                        throw Errors.InvalidItemSize();
                    consumed = buffer.GetPosition(sizeof(int));
                    buffer = buffer.Slice(consumed);
                    frameBuffer.Renew(_maxBufferSize);
                }

                // Phase 2: copy as many frame-body bytes as are available into frameBuffer
                var remaining = frameLength - frameBuffer.WrittenCount;
                if (remaining > 0 && buffer.Length > 0) {
                    var toCopy = (int)Math.Min(remaining, buffer.Length);
                    var span = frameBuffer.GetSpan(toCopy);
                    buffer.Slice(0, toCopy).CopyTo(span);
                    frameBuffer.Advance(toCopy);
                    consumed = buffer.GetPosition(toCopy);
                }

                reader.AdvanceTo(consumed);

                if (frameBuffer.WrittenCount < frameLength) {
                    // Need more data to complete the current frame.
                    if (result.IsCompleted)
                        yield break;
                    continue;
                }

                // Frame is complete - parse it out into messages.
                _meters.IncomingFrameSizeHistogram.Record(frameLength);
                var array = frameBuffer.Array;
                var len = frameLength;
                frameLength = -1; // next iteration starts a new frame
                var offset = 0;
                while (offset < len) { // Zero-length frames are skipped here
                    var message = tryDeserialize(array, ref offset, len);
                    if (message is not null)
                        yield return message;
                }
                // RpcInboundMessage.ArgumentData is consumed synchronously by the enumerator's caller,
                // so frameBuffer can be safely renewed on the next iteration.
            }
        }
        finally {
            frameBuffer.Dispose();
            _ = DisposeAsync();
        }
    }

    private static int ReadFrameLength(in ReadOnlySequence<byte> buffer)
    {
        var firstSpan = buffer.First.Span;
        if (firstSpan.Length >= sizeof(int))
            return RpcByteMessageSerializerV5.ReadLittleEndian(firstSpan);

        Span<byte> header = stackalloc byte[sizeof(int)];
        buffer.Slice(0, sizeof(int)).CopyTo(header);
        return RpcByteMessageSerializerV5.ReadLittleEndian((ReadOnlySpan<byte>)header);
    }

    // This method should never throw
    private void ClosePipe(Exception? error)
    {
        if (error is OperationCanceledException)
            error = null;

        try {
            PipeWriter.Complete(error);
        }
        catch {
            // Intended
        }
        try {
            PipeReader.Complete(error);
        }
        catch {
            // Intended
        }
    }

    // Nested types

    /// <summary>
    /// OpenTelemetry metrics for <see cref="RpcPipeTransport"/> operations.
    /// </summary>
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
            var ms = "rpc.pipe.transport";
            ChannelCounter = m.CreateObservableCounter($"{ms}.count",
                () => InterlockedExt.VolatileRead(ref ChannelCount),
                null, "Number of RpcPipeTransport instances.");
            IncomingItemCounter = m.CreateCounter<long>($"{ms}.incoming.item.count",
                null, "Number of items received via RpcPipeTransport.");
            OutgoingItemCounter = m.CreateCounter<long>($"{ms}.outgoing.item.count",
                null, "Number of items sent via RpcPipeTransport.");
            IncomingFrameSizeHistogram = m.CreateHistogram<int>($"{ms}.incoming.frame.size",
                "By", "RpcPipeTransport's incoming frame size in bytes.");
            OutgoingFrameSizeHistogram = m.CreateHistogram<int>($"{ms}.outgoing.frame.size",
                "By", "RpcPipeTransport's outgoing frame size in bytes.");
        }
    }
}
