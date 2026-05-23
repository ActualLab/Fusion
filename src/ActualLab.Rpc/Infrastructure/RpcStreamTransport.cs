using ActualLab.Collections;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// An <see cref="RpcTransport"/> implementation that sends and receives RPC messages over a
/// pair of <see cref="Stream"/>s. Each frame (a batch of messages) is prefixed with its
/// 4-byte little-endian length.
/// </summary>
public sealed class RpcStreamTransport : RpcFrameBasedTransport
{
    /// <summary>
    /// Configuration options for <see cref="RpcStreamTransport"/>.
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
    public Stream ReaderStream { get; }
    public Stream WriterStream { get; }
    // Owner is disposed (if it's IAsyncDisposable / IDisposable) once the transport is closed.
    public object? Owner { get; init; }
    public bool OwnsOwner { get; init; } = true;

    public RpcStreamTransport(
        Options settings,
        RpcPeer peer,
        Stream readerStream,
        Stream writerStream,
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
        ReaderStream = readerStream;
        WriterStream = writerStream;
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
        var stream = WriterStream;
#if NETSTANDARD2_0
#pragma warning disable CA1835 // Use Memory<byte> overload of WriteAsync
        if (!MemoryMarshal.TryGetArray(frame, out var frameSegment))
            frameSegment = new ArraySegment<byte>(frame.ToArray());
        await stream
            .WriteAsync(frameSegment.Array!, frameSegment.Offset, frameSegment.Count, StopToken)
            .ConfigureAwait(false);
#pragma warning restore CA1835
#else
        await stream.WriteAsync(frame, StopToken).ConfigureAwait(false);
#endif
        await stream.FlushAsync(StopToken).ConfigureAwait(false);
    }

    protected override async IAsyncEnumerable<RpcInboundMessage> ReadAll(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var bufferSize = Settings.BufferSize;
        using var commonCts = cancellationToken.LinkWith(StopToken);
        var stream = ReaderStream;
        var ct = commonCts.Token;
        var tryDeserialize = Codec.TryDeserialize;
        var buffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, bufferSize, mustClear: false);
        try {
            while (true) {
                // Read more bytes into the buffer (appending past WrittenCount)
                buffer.EnsureCapacity(bufferSize); // ensures at least bufferSize free space
                int read;
                try {
#if NETSTANDARD2_0
#pragma warning disable CA1835 // Use Memory<byte> overload of WriteAsync
                    var freeSegment = buffer.FreeArraySegment;
                    read = await stream
                        .ReadAsync(freeSegment.Array!, freeSegment.Offset, freeSegment.Count, ct)
                        .ConfigureAwait(false);
#pragma warning restore CA1835
#else
                    read = await stream.ReadAsync(buffer.FreeMemory, ct).ConfigureAwait(false);
#endif
                }
                catch (Exception e) when (e.IsCancellationOf(ct)) {
                    yield break;
                }
                catch (Exception e) {
                    Log?.LogWarning(e, "Stream.ReadAsync failed");
                    yield break;
                }
                if (read == 0)
                    yield break; // End of stream
                buffer.Advance(read);

                // Parse as many complete frames as are available in the buffer
                var dataStart = 0;
                while (true) {
                    var available = buffer.WrittenCount - dataStart;
                    if (available < Int32Size)
                        break; // need more bytes for the length header

                    var frameLength = buffer.Array.AsSpan(dataStart, Int32Size).ReadLittleEndian();
                    if (frameLength <= 0 || frameLength > Settings.MaxFrameSize)
                        throw Errors.InvalidItemSize();
                    if (available < Int32Size + frameLength)
                        break; // need more bytes for the frame body

                    Meters.IncomingFrameSizeHistogram.Record(frameLength);
                    var bodyEnd = dataStart + Int32Size + frameLength;
                    var offset = dataStart + Int32Size;
                    while (offset < bodyEnd) { // Zero-length frames are skipped here
                        var message = tryDeserialize(buffer.Array, ref offset, bodyEnd);
                        if (message is not null)
                            yield return message;
                    }
                    dataStart = bodyEnd;
                }

                // The code that uses frames' data (RpcInboundMessage.ArgumentData) is running synchronously,
                // so by the time we are here the parsed bytes in the buffer can be reused.
                if (dataStart == buffer.WrittenCount) {
                    // All bytes consumed - Renew lets the pool recycle if the array is oversized
                    buffer.Renew(Settings.MaxBufferSize);
                }
                else if (dataStart > 0) {
                    // Partial leftover from a yet-incomplete frame - shift it to the front
                    var leftover = buffer.WrittenCount - dataStart;
                    Array.Copy(buffer.Array, dataStart, buffer.Array, 0, leftover);
                    buffer.Position = leftover;
                }
            }
        }
        finally {
            buffer.Dispose();
            _ = DisposeAsync();
        }
    }

    // Nested types

    /// <summary>
    /// OpenTelemetry metrics for <see cref="RpcStreamTransport"/> operations.
    /// </summary>
    public class MeterSet() : FrameMeterSet("stream", "RpcStreamTransport");
}
