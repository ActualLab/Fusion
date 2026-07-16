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
        public int MaxMessageSize { get; init; } = RpcTextMessageSerializerV3.GetMaxMessageSize(
            Math.Max(
                RpcTextMessageSerializer.Defaults.MaxArgumentDataSize,
                RpcByteMessageSerializer.Defaults.MaxArgumentDataSize));
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
        if (settings.MaxMessageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "settings.MaxMessageSize must be positive.");

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
        var maxMessageSize = Settings.MaxMessageSize;
        var overflowBuffer = new byte[1];
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
                var remainingCapacity = maxMessageSize - buffer.WrittenCount;
                var isOverflowProbe = remainingCapacity == 0;
#if NETSTANDARD2_0
                ArraySegment<byte> receiveBuffer;
#else
                Memory<byte> receiveBuffer;
#endif
                if (isOverflowProbe) {
#if NETSTANDARD2_0
                    receiveBuffer = new ArraySegment<byte>(overflowBuffer);
#else
                    receiveBuffer = overflowBuffer;
#endif
                }
                else {
                    var requestedCapacity = Math.Min(
                        Math.Max(bufferSize, buffer.FreeCapacity),
                        remainingCapacity);
                    _ = buffer.GetMemory(requestedCapacity);
#if NETSTANDARD2_0
                    receiveBuffer = new ArraySegment<byte>(buffer.Array, buffer.WrittenCount, requestedCapacity);
#else
                    receiveBuffer = buffer.Array.AsMemory(buffer.WrittenCount, requestedCapacity);
#endif
                }
                int count;
                WebSocketMessageType messageType;
                bool endOfMessage;
                try {
                    var r = await WebSocket.ReceiveAsync(receiveBuffer, CancellationToken.None).ConfigureAwait(false);
                    (count, messageType, endOfMessage) = (r.Count, r.MessageType, r.EndOfMessage);
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
                    if (e is not WebSocketException { WebSocketErrorCode: WebSocketError.ConnectionClosedPrematurely })
                        throw;

                    (count, messageType, endOfMessage) = (0, WebSocketMessageType.Close, true);
                }
                if (messageType == WebSocketMessageType.Close) {
                    if (WebSocket.CloseStatus.HasValue
                        && (int)WebSocket.CloseStatus.Value == RpcWebSocketCloseCode.UnsupportedFormat)
                        throw Errors.UnsupportedSerializationFormat(WebSocket.CloseStatusDescription.NullIfEmpty());
                    yield break;
                }
                if (messageType != MessageType)
                    throw Errors.InvalidWebSocketMessageType(messageType, MessageType);

                if (isOverflowProbe && count != 0) {
                    Meters.IncomingFrameSizeHistogram.Record(count);
                    await CloseMessageTooLarge().ConfigureAwait(false);
                    yield break;
                }
                buffer.Advance(count);
                Meters.IncomingFrameSizeHistogram.Record(count);
                if (!endOfMessage)
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

    private async Task CloseMessageTooLarge()
    {
        if (WebSocket.State is WebSocketState.Closed or WebSocketState.Aborted)
            return;

        try {
            await WebSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message too large.", default)
                .WaitAsync(Settings.CloseTimeout, CancellationToken.None)
                .SilentAwait(false);
        }
        catch {
            // Intended
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
