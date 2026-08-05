using System.Diagnostics.Metrics;
using ActualLab.Channels;
using ActualLab.Concurrency;
using ActualLab.Rpc.Compression;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Base class for transports that batch outbound RPC messages into frames.
/// </summary>
public abstract class RpcFrameBasedTransport : RpcTransport
{
    // This constant is used only in the calculation of DefaultMaxFrameSize below.
    // The ArrayPool bucket every transport's receive buffer is meant to land in. ArrayPoolBuffer rounds
    // every capacity request up to the next power of two, so one byte above a bucket boundary doubles the
    // allocation - which is why the frame limit is defined relative to a bucket rather than freely.
    public const int MaxFrameSizeBucket = 1 << 24; // 16 MiB,

    // The largest frame (= batch of messages) any transport sends or accepts. Two constraints shape it:
    // - It must fit one maximum-size message: MaxArgumentDataSize (15.5 MiB) plus the worst-case envelope
    //   of the most expensive registered format, RpcTextMessageSerializerV3.MaxEnvelopeSize (244,297
    //   = 259 B of syntax + 6x JSON escaping of a 1 KiB method ref and 31 headers of 255 B + 1 KiB)
    //   plus its delimiter, i.e. 16,497,226 bytes total.
    //   RpcWebSocketTransportSizeTest.MaxArgumentDataSizeMessageFitsMaxFrameSizeInEveryFormat guards this.
    // - It must leave room, inside MaxFrameSizeBucket, for what RpcStreamTransport buffers alongside the
    //   frame itself: its 4-byte length prefix plus up to BufferSize of read-ahead. Hence the 64 KiB
    //   reserve - without it a maximum-size frame would push that transport into the next 32 MiB bucket.
    public const int DefaultMaxFrameSize = MaxFrameSizeBucket - 65_536; // 16,711,680
    // Until the handshake is received the peer is anonymous, and the only message it may legitimately
    // send is the handshake itself - two GUIDs, a version set, and two ints, which measures under 1 KB
    // in every registered format (see RpcWebSocketTransportSizeTest.HandshakeFitsPreHandshakeLimitInEveryFormat).
    public const int DefaultMaxPreHandshakeFrameSize = 16_384;

    protected const int Int32Size = sizeof(int);

    private readonly int _frameSize;
    private readonly int _maxBufferSize;
    private readonly int _maxFrameSize;
    // Both come from the peer's serialization format, and the two directions are independent -
    // so either can be null on its own
    private readonly RpcFrameEncoder? _frameEncoder;
    private readonly RpcFrameDecoder? _frameDecoder;
    // Smaller than _maxFrameSize by the worst-case compression overhead when there is an encoder,
    // so that an encoded frame still fits the limit the peer enforces on the wire
    private readonly int _maxPayloadSize;
    private readonly Channel<RpcOutboundMessage> _writeChannel;
    private readonly ChannelWriter<RpcOutboundMessage> _writeChannelWriter;
    private readonly AsyncTaskMethodBuilder _whenCompletedSource;
    private readonly Task _whenCompleted;
    private readonly RpcFrameDelayer? _frameDelayer;
    private ArrayPoolBuffer<byte> _writeBuffer;
    private ArrayPoolBuffer<byte> _flushingBuffer;
    private Task? _whenClosed;
    private int _getAsyncEnumeratorCounter;

    protected FrameMeterSet Meters { get; }
    protected RpcFrameCodec Codec { get; }
    // A peer that compresses nothing outbound writes exactly the frames it did before compression
    // existed, whatever it decodes inbound.
    protected bool HasFrameEncoder => _frameEncoder is not null;
    protected bool HasFrameDecoder => _frameDecoder is not null;
    private int WriteFrameLength {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _writeBuffer.WrittenCount - Int32Size;
    }

    public RpcMessageSerializer MessageSerializer { get; }
    public override Task WhenCompleted => _whenCompleted;
    public Task WhenClosed => _whenClosed ?? Task.CompletedTask;
    public ILogger? Log { get; }
    public ILogger? ErrorLog { get; }

    protected RpcFrameBasedTransport(
        RpcPeer peer,
        CancellationTokenSource? stopTokenSource,
        int frameSize,
        int bufferSize,
        int maxBufferSize,
        int maxFrameSize,
        Func<RpcFrameDelayer?>? frameDelayerFactory,
        ChannelOptions writeChannelOptions,
        FrameMeterSet meters,
        IServiceProvider? logServices = null)
        : base(peer, stopTokenSource)
    {
        MessageSerializer = peer.MessageSerializer;
        Meters = meters;
        Log = (logServices ?? peer.Hub.Services).LogFor(GetType());
        ErrorLog = Log.IfEnabled(LogLevel.Error);

        _whenCompletedSource = AsyncTaskMethodBuilderExt.New();
        _whenCompleted = _whenCompletedSource.Task;

        _frameSize = frameSize;
        if (_frameSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameSize), "Frame size must be positive.");
        _maxFrameSize = maxFrameSize;
        // The upper bound is what's left of the header word after the two compression flags
        if (_maxFrameSize <= 0 || _maxFrameSize > RpcFrameCodec.MaxFrameSize)
            throw new ArgumentOutOfRangeException(nameof(maxFrameSize),
                $"Max frame size must be within 1..{RpcFrameCodec.MaxFrameSize}.");
        _maxBufferSize = maxBufferSize;

        // Compression is a property of the peer's serialization format, so both ends know it
        // before the first frame - there is nothing to negotiate and no arming step.
        if (peer.InboundCompression is { } inbound)
            _frameDecoder = new RpcFrameDecoder(
                inbound.DecompressorFactory.Invoke(), bufferSize, maxBufferSize);
        if (peer.OutboundCompression is { } outbound) {
            _frameEncoder = new RpcFrameEncoder(
                outbound.CompressorFactory.Invoke(), outbound.Options, bufferSize, maxBufferSize);
            // What the peer size-checks is the encoded frame, so the payload budget is the largest
            // one whose worst-case encoding still fits - which only the codec can say
            _maxPayloadSize = _frameEncoder.GetMaxPayloadSize(_maxFrameSize);
        }
        else
            _maxPayloadSize = _maxFrameSize;

        _frameDelayer = frameDelayerFactory?.Invoke();
        Codec = new RpcFrameCodec(
            MessageSerializer, Meters.IncomingItemCounter, Meters.OutgoingItemCounter, ErrorLog, Int32Size);
        _writeBuffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, bufferSize, mustClear: false);
        _flushingBuffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, bufferSize, mustClear: false);
        ResetWriteBuffer(_writeBuffer);
        ResetWriteBuffer(_flushingBuffer);

        _writeChannel = ChannelExt.Create<RpcOutboundMessage>(writeChannelOptions);
        _writeChannelWriter = _writeChannel.Writer;
    }

    public override void Send(RpcOutboundMessage message, CancellationToken cancellationToken = default)
    {
        if (_writeChannelWriter.TryWrite(message))
            return;

        _ = Write(message, cancellationToken);
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
            ? ReadAll(cancellationToken).GetAsyncEnumerator(cancellationToken)
            : throw ActualLab.Internal.Errors.AlreadyInvoked($"{GetType().GetName()}.GetAsyncEnumerator");

    // Protected methods

    protected void Start()
    {
        using var __ = ExecutionContextExt.TrySuppressFlow();
        _whenClosed = Task.Run(async () => {
            Interlocked.Increment(ref Meters.ChannelCount);
            try {
                var whenStopped = TaskExt.NeverEnding(StopToken);
                var whenWriterCompleted = Task.Run(RunWriter, CancellationToken.None);
                await Task.WhenAny(whenStopped, _whenCompleted, whenWriterCompleted).SilentAwait(false);

                StopTokenSource.CancelSilently();
                TryComplete();
                await whenWriterCompleted.ConfigureAwait(false);
                await _whenCompleted.SilentAwait(false);

                while (_writeChannel.Reader.TryRead(out var message))
                    CompleteSend(message, new ChannelClosedException());

                await CloseTransport(null).ConfigureAwait(false);

                _flushingBuffer.Dispose();
                _writeBuffer.Dispose();
                _frameEncoder?.Dispose();
                _frameDecoder?.Dispose();
            }
            catch (Exception e) {
                Log?.LogError(e, "Error in {Transport}.WhenClosed, this should never happen", GetType().GetName());
            }
            finally {
                Interlocked.Decrement(ref Meters.ChannelCount);
            }
        }, default);
    }

    protected override async Task DisposeAsyncCore()
        => await WhenClosed.ConfigureAwait(false);

    protected abstract Task WriteFrame(ReadOnlyMemory<byte> frame);

    protected abstract IAsyncEnumerable<RpcInboundMessage> ReadAll(CancellationToken cancellationToken = default);

    // Returns the array and range a frame's messages should be read from - the frame itself when
    // this direction is uncompressed, the decoded copy of it otherwise.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected (byte[] Array, int Offset, int End) DecodeFrame(int header, byte[] array, int offset, int end)
        => _frameDecoder is { } decoder
            ? decoder.Decode(header, array, offset, end, _maxFrameSize)
            : (array, offset, end);

    protected virtual Task CloseTransport(Exception? error)
        => Task.CompletedTask;

    // Private methods

    private async Task Write(RpcOutboundMessage message, CancellationToken cancellationToken)
    {
        try {
            await _writeChannelWriter.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) {
            CompleteSend(message, e);
        }
    }

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
            while (await reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false)) {
                while (reader.TryRead(out var message)) {
                    SerializeAndCompleteSend(message);
                    if (WriteFrameLength >= _frameSize) {
                        await lastFlushTask.ConfigureAwait(false);
                        lastFlushTask = FlushFrame();
                    }
                }

                if (WriteFrameLength != 0) {
                    await lastFlushTask.ConfigureAwait(false);
                    lastFlushTask = FlushFrame();
                }
            }
            await lastFlushTask.ConfigureAwait(false);
        }
        catch (Exception e) {
            if (!e.IsCancellationOf(StopToken))
                error = e;
        }
        finally {
            // The write/flush buffers are disposed once this method completes,
            // so an in-flight WriteFrame must be awaited no matter how we exit
            await lastFlushTask.SilentAwait(false);
            TryComplete(error);
        }
    }

    private async Task RunWriterWithFrameDelayer(RpcFrameDelayer frameDelayer)
    {
        Task? whenMustFlush = null;
        Task lastFlushTask = Task.CompletedTask;
        Task<bool>? waitToReadTask = null;
        var reader = _writeChannel.Reader;

        try {
            while (true) {
                if (whenMustFlush is not null) {
                    if (whenMustFlush.IsCompleted) {
                        if (WriteFrameLength != 0) {
                            await lastFlushTask.ConfigureAwait(false);
                            lastFlushTask = FlushFrame();
                        }
                        whenMustFlush = null;
                    }
                    else {
                        waitToReadTask ??= reader.WaitToReadAsync(CancellationToken.None).AsTask();
                        await Task.WhenAny(whenMustFlush, waitToReadTask).ConfigureAwait(false);
                        if (!waitToReadTask.IsCompleted)
                            continue;
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
                    break;

                while (reader.TryRead(out var message)) {
                    SerializeAndCompleteSend(message);
                    if (WriteFrameLength >= _frameSize) {
                        await lastFlushTask.ConfigureAwait(false);
                        lastFlushTask = FlushFrame();
                        whenMustFlush = null;
                    }
                }
                if (whenMustFlush is null && WriteFrameLength > 0)
                    whenMustFlush = frameDelayer.Invoke(WriteFrameLength);
            }

            if (WriteFrameLength != 0) {
                await lastFlushTask.ConfigureAwait(false);
                lastFlushTask = FlushFrame();
            }
            await lastFlushTask.ConfigureAwait(false);
        }
        finally {
            // The write/flush buffers are disposed once the writer completes,
            // so an in-flight WriteFrame must be awaited no matter how we exit
            await lastFlushTask.SilentAwait(false);
        }
    }

    private void SerializeAndCompleteSend(RpcOutboundMessage message)
    {
        var startOffset = _writeBuffer.WrittenCount;
        try {
            Codec.Serialize(message, _writeBuffer);
        }
        catch (Exception e) {
            CompleteSend(message, e);
            return;
        }
        if (WriteFrameLength > _maxPayloadSize) {
            // The receiving peer would drop the connection over this frame, so fail the call locally
            // instead - a locally failed call isn't retried, an aborted connection's calls are
            _writeBuffer.Position = startOffset;
            CompleteSend(message, ActualLab.Internal.Errors.SizeLimitExceeded("Message"));
            return;
        }

        CompleteSend(message);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Task FlushFrame()
    {
        (_flushingBuffer, _writeBuffer) = (_writeBuffer, _flushingBuffer);
        var frameLength = _flushingBuffer.WrittenCount - Int32Size;
        var frame = _flushingBuffer.WritableWrittenMemory;
        ResetWriteBuffer(_writeBuffer);

        Meters.OutgoingFrameSizeHistogram.Record(frameLength);
        if (_frameEncoder is { } encoder)
            return WriteFrame(encoder.Encode(frame));

        frame.Span.WriteLittleEndian(frameLength);
        return WriteFrame(frame);
    }

    private void ResetWriteBuffer(ArrayPoolBuffer<byte> buffer)
    {
        buffer.Renew(_maxBufferSize);
        buffer.Advance(Int32Size);
    }

    // Nested types

    public abstract class FrameMeterSet
    {
        public readonly ObservableGauge<long> ChannelCounter;
        public readonly Counter<long> IncomingItemCounter;
        public readonly Counter<long> OutgoingItemCounter;
        public readonly Histogram<int> IncomingFrameSizeHistogram;
        public readonly Histogram<int> OutgoingFrameSizeHistogram;
        public long ChannelCount;

        protected FrameMeterSet(string name, string descriptionName)
        {
            var m = RpcInstruments.Meter;
            var ms = $"rpc.{name}.transport";
            ChannelCounter = m.CreateObservableGauge($"{ms}.count",
                () => InterlockedExt.VolatileRead(ref ChannelCount),
                null, $"Number of {descriptionName} instances.");
            IncomingItemCounter = m.CreateCounter<long>($"{ms}.incoming.item.count",
                null, $"Number of items received via {descriptionName}.");
            OutgoingItemCounter = m.CreateCounter<long>($"{ms}.outgoing.item.count",
                null, $"Number of items sent via {descriptionName}.");
            IncomingFrameSizeHistogram = m.CreateHistogram<int>($"{ms}.incoming.frame.size",
                "By", $"{descriptionName}'s incoming frame size in bytes.");
            OutgoingFrameSizeHistogram = m.CreateHistogram<int>($"{ms}.outgoing.frame.size",
                "By", $"{descriptionName}'s outgoing frame size in bytes.");
        }
    }
}
