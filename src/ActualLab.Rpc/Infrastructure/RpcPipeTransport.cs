using System.Buffers;
using System.IO.Pipelines;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// An <see cref="RpcTransport"/> implementation that sends and receives RPC messages over a
/// <see cref="System.IO.Pipelines.PipeReader"/> / <see cref="System.IO.Pipelines.PipeWriter"/> pair. Unlike a WebSocket, a pipe is a raw
/// byte stream, so each frame (a batch of messages) is prefixed with its 4-byte little-endian length.
/// </summary>
public sealed class RpcPipeTransport : RpcFrameBasedTransport
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

    public Options Settings { get; }
    public PipeReader PipeReader { get; }
    public PipeWriter PipeWriter { get; }
    // Owner is disposed (if it's IAsyncDisposable / IDisposable) once the transport is closed.
    public object? Owner { get; init; }
    public bool OwnsOwner { get; init; } = true;

    public RpcPipeTransport(
        Options settings,
        RpcPeer peer,
        PipeReader pipeReader,
        PipeWriter pipeWriter,
        CancellationTokenSource? stopTokenSource = null)
        : base(
            peer,
            stopTokenSource,
            settings.FrameSize,
            settings.BufferSize,
            settings.MaxBufferSize,
            settings.FrameDelayerFactory,
            settings.WriteChannelOptions,
            StaticMeters)
    {
        Settings = settings;
        PipeReader = pipeReader;
        PipeWriter = pipeWriter;
        Start();
    }

    protected override async Task DisposeAsyncCore()
    {
        await base.DisposeAsyncCore().ConfigureAwait(false);
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

    // Protected/internal methods

    protected override async Task WriteFrame(ReadOnlyMemory<byte> frame)
    {
        var writer = PipeWriter;
        var span = writer.GetSpan(frame.Length);
        frame.Span.CopyTo(span);
        writer.Advance(frame.Length);
        var flushResult = await writer.FlushAsync(StopToken).ConfigureAwait(false);
        if (flushResult.IsCompleted || flushResult.IsCanceled)
            throw new ChannelClosedException();
    }

    protected override async IAsyncEnumerable<RpcInboundMessage> ReadAll(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var commonCts = cancellationToken.LinkWith(StopToken);
        var reader = PipeReader;

        var tryDeserialize = Codec.TryDeserialize;
        var frameBuffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, Settings.BufferSize, mustClear: false);
        var frameLength = -1; // -1 = length header not read yet; >= 0 = bytes still being accumulated into frameBuffer
        try {
            while (true) {
                ReadResult result = default;
                var isDone = false;
                try {
                    result = await reader.ReadAsync(commonCts.Token).ConfigureAwait(false);
                }
                catch (Exception e) when (e.IsCancellationOf(commonCts.Token)) {
                    isDone = true;
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
                    if (buffer.Length < Int32Size) {
                        if (result.IsCompleted)
                            yield break;
                        reader.AdvanceTo(buffer.Start, buffer.End); // need more data
                        continue;
                    }
                    frameLength = ReadFrameLength(buffer);
                    if (frameLength <= 0 || frameLength > Settings.MaxFrameSize)
                        throw Errors.InvalidItemSize();
                    consumed = buffer.GetPosition(Int32Size);
                    buffer = buffer.Slice(consumed);
                    frameBuffer.Renew(Settings.MaxBufferSize);
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
                Meters.IncomingFrameSizeHistogram.Record(frameLength);
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
        if (firstSpan.Length >= Int32Size)
            return firstSpan.ReadLittleEndian();

        Span<byte> header = stackalloc byte[Int32Size];
        buffer.Slice(0, Int32Size).CopyTo(header);
        return ((ReadOnlySpan<byte>)header).ReadLittleEndian();
    }

    // This method should never throw
    protected override Task CloseTransport(Exception? error)
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
        return Task.CompletedTask;
    }

    // Nested types

    /// <summary>
    /// OpenTelemetry metrics for <see cref="RpcPipeTransport"/> operations.
    /// </summary>
    public class MeterSet() : FrameMeterSet("pipe", "RpcPipeTransport");
}
