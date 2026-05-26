using System.Net.WebSockets;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.WebSockets;

/// <summary>
/// An <see cref="RpcTransport"/> implementation that sends and receives RPC messages over a WebSocket connection.
/// </summary>
public sealed class RpcWebSocketTransport : RpcFrameBasedTransport
{
    /// <summary>
    /// Configuration options for <see cref="RpcWebSocketTransport"/>.
    /// </summary>
    public record Options
    {
        public static readonly Options Default = new();

        public Func<RpcFrameDelayer?>? FrameDelayerFactory { get; init; } = RpcFrameDelayerFactories.None;

        public int FrameSize { get; init; } = 12_000; // 8 x 1500 (min. MTU minus some reserve)
        public int BufferSize { get; init; } = 16_000;
        public int MaxBufferSize { get; init; } = 256_000;
        // High CloseTimeout values "shrink" effective ConnectTimeout,
        // low values increase abrupt/graceful close ratio, which is a no-op in our case.
        public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(1);

        // Use of UnboundedChannelOptions is totally fine here: if the message is enqueued
        public ChannelOptions WriteChannelOptions { get; init; } = new UnboundedChannelOptions() {
            // FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true, // Must be true
            SingleWriter = false, // Must be false
            AllowSynchronousContinuations = false, // Must be false, setting it to true will kill the throughput!
        };
    }

    private static readonly MeterSet StaticMeters = new();

    public bool OwnsWebSocketOwner { get; set; } = true;
    public Options Settings { get; }
    public WebSocketOwner WebSocketOwner { get; }
    public WebSocket WebSocket { get; }
    public bool IsTextSerializer { get; }
    public WebSocketMessageType MessageType { get; }

    public RpcWebSocketTransport(
        Options settings,
        RpcPeer peer,
        WebSocketOwner webSocketOwner,
        CancellationTokenSource? stopTokenSource = null)
        : base(
            peer,
            stopTokenSource,
            settings.FrameSize,
            settings.BufferSize,
            settings.MaxBufferSize,
            settings.FrameDelayerFactory,
            settings.WriteChannelOptions,
            StaticMeters,
            logServices: webSocketOwner.Services)
    {
        Settings = settings;
        WebSocketOwner = webSocketOwner;
        WebSocket = webSocketOwner.WebSocket;
        IsTextSerializer = MessageSerializer is RpcTextMessageSerializer;
        MessageType = IsTextSerializer
            ? WebSocketMessageType.Text
            : WebSocketMessageType.Binary;
        Start();
    }

    protected override async Task DisposeAsyncCore()
    {
        await base.DisposeAsyncCore().ConfigureAwait(false);
        if (OwnsWebSocketOwner)
            await WebSocketOwner.DisposeAsync().ConfigureAwait(false);
    }

    // Protected/internal methods

    protected override Task WriteFrame(ReadOnlyMemory<byte> frame)
        => WebSocket.SendAsync(frame[Int32Size..], MessageType, endOfMessage: true, CancellationToken.None).AsTask();

    protected override async IAsyncEnumerable<RpcInboundMessage> ReadAll(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var bufferSize = Settings.BufferSize;
        using var commonCts = cancellationToken.LinkWith(StopToken);
        // ReSharper disable once UseAwaitUsing
        using var commonTokenRegistration = commonCts.Token.Register(
            static x => AbortWebSocket((WebSocket)x!),
            WebSocket);

        // Start with a non-pooled buffer for initial reads
        var buffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, bufferSize, mustClear: false);
        var tryDeserialize = Codec.TryDeserialize;

        try {
            while (true) {
                var requestedCapacity = Math.Max(bufferSize, buffer.FreeCapacity);
                var readMemory = buffer.GetMemory(requestedCapacity);
                var arraySegment = new ArraySegment<byte>(buffer.Array, buffer.WrittenCount, readMemory.Length);
                WebSocketReceiveResult r;
                try {
                    r = await WebSocket.ReceiveAsync(arraySegment, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception e) {
                    if (commonCts.IsCancellationRequested) {
                        // We don't pass commonCts.Token to ReceiveAsync to speed it up, so there is no standard
                        // cancellation. We abort WebSocket on commonCts cancellation instead, which makes
                        // ReceiveAsync to throw. So any error from it after commonCts cancellation is an expected
                        // outcome indicating that cancellation happened.
                        // And we obviously don't want to log this exception as it's expected behavior.
                        break;
                    }

                    Log?.LogWarning(e, "WebSocket.ReceiveAsync failed");
                    if (e is WebSocketException { WebSocketErrorCode: WebSocketError.ConnectionClosedPrematurely })
                        r = new WebSocketReceiveResult(0, WebSocketMessageType.Close, endOfMessage: true);
                    else
                        throw;
                }
                if (r.MessageType == WebSocketMessageType.Close) {
                    if (WebSocket.CloseStatus.HasValue
                        && (int)WebSocket.CloseStatus.Value == RpcWebSocketCloseCode.UnsupportedFormat)
                        throw Errors.UnsupportedSerializationFormat(WebSocket.CloseStatusDescription.NullIfEmpty());
                    yield break;
                }
                if (r.MessageType != MessageType)
                    throw Errors.InvalidWebSocketMessageType(r.MessageType, MessageType);

                buffer.Advance(r.Count);
                Meters.IncomingFrameSizeHistogram.Record(r.Count);
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
                buffer.Renew(Settings.MaxBufferSize);
            }
        }
        finally {
            buffer.Dispose();
            _ = DisposeAsync();
        }
    }

    // This method should never throw
    protected override async Task CloseTransport(Exception? error)
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
        if (WebSocket.State is not (WebSocketState.Closed or WebSocketState.Aborted))
            AbortWebSocket(WebSocket);
    }

    private static void AbortWebSocket(WebSocket webSocket)
    {
        try {
            if (webSocket.State is not (WebSocketState.Closed or WebSocketState.Aborted))
                webSocket.Abort();
        }
        catch {
            // Intended
        }
    }

    // Nested types

    /// <summary>
    /// OpenTelemetry metrics for <see cref="RpcWebSocketTransport"/> operations.
    /// </summary>
    public class MeterSet() : FrameMeterSet("ws", "WebSocketRpcTransport");
}
