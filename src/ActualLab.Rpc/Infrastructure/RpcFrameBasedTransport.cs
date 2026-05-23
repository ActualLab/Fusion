using System.Diagnostics.Metrics;
using ActualLab.Channels;
using ActualLab.Concurrency;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Base class for transports that batch outbound RPC messages into frames.
/// </summary>
public abstract class RpcFrameBasedTransport : RpcTransport
{
    protected const int Int32Size = sizeof(int);

    private readonly int _frameSize;
    private readonly int _maxBufferSize;
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
        _maxBufferSize = maxBufferSize;

        _frameDelayer = frameDelayerFactory?.Invoke();
        Codec = new RpcFrameCodec(
            MessageSerializer, Meters.IncomingItemCounter, Meters.OutgoingItemCounter, ErrorLog);
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

    protected virtual Task CloseTransport(Exception? error)
        => Task.CompletedTask;

    // Private methods

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
            var serialize = Codec.Serialize;
            while (await reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false)) {
                while (reader.TryRead(out var message)) {
                    try {
                        serialize(message, _writeBuffer);
                        CompleteSend(message);
                    }
                    catch (Exception e) {
                        CompleteSend(message, e);
                    }
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
            TryComplete(error);
        }
    }

    private async Task RunWriterWithFrameDelayer(RpcFrameDelayer frameDelayer)
    {
        Task? whenMustFlush = null;
        Task lastFlushTask = Task.CompletedTask;
        Task<bool>? waitToReadTask = null;
        var reader = _writeChannel.Reader;
        var serialize = Codec.Serialize;

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
                try {
                    serialize(message, _writeBuffer);
                    CompleteSend(message);
                }
                catch (Exception e) {
                    CompleteSend(message, e);
                    continue;
                }

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Task FlushFrame()
    {
        (_flushingBuffer, _writeBuffer) = (_writeBuffer, _flushingBuffer);
        var frameLength = _flushingBuffer.WrittenCount - Int32Size;
        var frame = _flushingBuffer.WritableWrittenMemory;
        frame.Span.WriteLittleEndian(frameLength);
        ResetWriteBuffer(_writeBuffer);

        Meters.OutgoingFrameSizeHistogram.Record(frameLength);
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
        public readonly ObservableCounter<long> ChannelCounter;
        public readonly Counter<long> IncomingItemCounter;
        public readonly Counter<long> OutgoingItemCounter;
        public readonly Histogram<int> IncomingFrameSizeHistogram;
        public readonly Histogram<int> OutgoingFrameSizeHistogram;
        public long ChannelCount;

        protected FrameMeterSet(string name, string descriptionName)
        {
            var m = RpcInstruments.Meter;
            var ms = $"rpc.{name}.transport";
            ChannelCounter = m.CreateObservableCounter($"{ms}.count",
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
