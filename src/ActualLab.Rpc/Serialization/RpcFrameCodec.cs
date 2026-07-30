using System.Diagnostics.Metrics;
using ActualLab.Rpc.Infrastructure;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Serialization;

/// <summary>
/// Serializes a batch of RPC messages into a frame buffer and parses messages back out of one.
/// Shared by <see cref="WebSockets.RpcWebSocketTransport"/> and <see cref="Infrastructure.RpcPipeTransport"/>.
/// </summary>
public sealed class RpcFrameCodec
{
    public delegate void SerializeDelegate(RpcOutboundMessage message, ArrayPoolBuffer<byte> buffer);
    public delegate RpcInboundMessage? TryDeserializeDelegate(byte[] array, ref int offset, int totalLength);

    // Text message delimiters (matches master branch WebSocketChannelImpl)
    private const byte LineFeed = 0x0A; // LF
    private const byte RecordSeparator = 0x1E; // RS
    private const int Int32Size = sizeof(int);

    private readonly RpcMessageSerializerReadFunc _readFunc;
    private readonly RpcMessageSerializerWriteFunc _writeFunc;
    private readonly int _frameHeaderSize;
    private readonly Counter<long>? _incomingItemCounter;
    private readonly Counter<long>? _outgoingItemCounter;
    private readonly ILogger? _errorLog;

    public RpcMessageSerializer Serializer { get; }
    public bool IsTextSerializer { get; }
    public SerializeDelegate Serialize { get; }
    public TryDeserializeDelegate TryDeserialize { get; }

    // frameHeaderSize is how many bytes the transport reserves in the write buffer before the first
    // message - text framing needs it to tell "first message in this frame" from "buffer isn't empty".
    public RpcFrameCodec(
        RpcMessageSerializer serializer,
        Counter<long> incomingItemCounter,
        Counter<long> outgoingItemCounter,
        ILogger? errorLog = null,
        int frameHeaderSize = 0)
    {
        Serializer = serializer;
        IsTextSerializer = serializer is RpcTextMessageSerializer;

        _readFunc = serializer.ReadFunc;
        _writeFunc = serializer.WriteFunc;
        _frameHeaderSize = frameHeaderSize;
        _incomingItemCounter = incomingItemCounter.IfEnabled();
        _outgoingItemCounter = outgoingItemCounter.IfEnabled();
        _errorLog = errorLog;

        Serialize = IsTextSerializer
            ? SerializeText
            : serializer.PersistsMessageSize ? SerializeBinaryWithSize : SerializeBinary;
        TryDeserialize = IsTextSerializer
            ? TryDeserializeText
            : serializer.PersistsMessageSize ? TryDeserializeBinaryWithSize : TryDeserializeBinary;
    }

    // Private methods

    private void SerializeBinary(RpcOutboundMessage message, ArrayPoolBuffer<byte> buffer)
    {
        _outgoingItemCounter?.Add(1);
        var startOffset = buffer.WrittenCount;
        try {
            _writeFunc.Invoke(buffer, message);
        }
        catch (Exception e) {
            buffer.Position = startOffset;
            _errorLog?.LogError(e, "Couldn't serialize the outbound message: {Message}", message);
            throw;
        }
    }

    private void SerializeBinaryWithSize(RpcOutboundMessage message, ArrayPoolBuffer<byte> buffer)
    {
        _outgoingItemCounter?.Add(1);
        var startOffset = buffer.WrittenCount;
        try {
            // Binary format: use 4-byte size prefix
            buffer.GetSpan(64);
            buffer.Advance(4);
            _writeFunc.Invoke(buffer, message);
            var size = buffer.WrittenCount - startOffset;
            buffer.Array.AsSpan(startOffset).WriteLittleEndian(size);
        }
        catch (Exception e) {
            buffer.Position = startOffset;
            _errorLog?.LogError(e, "Couldn't serialize the outbound message: {Message}", message);
            throw;
        }
    }

    private void SerializeText(RpcOutboundMessage message, ArrayPoolBuffer<byte> buffer)
    {
        _outgoingItemCounter?.Add(1);
        var startOffset = buffer.WrittenCount;
        try {
            // Text format: use LF+RS delimiter between messages (no size prefix).
            // Comparing against the frame header size rather than 0 matters: the transport has
            // already advanced past the reserved header, so the frame's first message would
            // otherwise get a leading delimiter that the reader sees as an empty message.
            if (startOffset != _frameHeaderSize) {
                var delimiterSpan = buffer.GetSpan(2);
                delimiterSpan[0] = LineFeed;
                delimiterSpan[1] = RecordSeparator;
                buffer.Advance(2);
            }
            _writeFunc.Invoke(buffer, message);
        }
        catch (Exception e) {
            buffer.Position = startOffset;
            _errorLog?.LogError(e, "Couldn't serialize the outbound message: {Message}", message);
            throw;
        }
    }

    private RpcInboundMessage? TryDeserializeBinary(byte[] array, ref int offset, int totalLength)
    {
        _incomingItemCounter?.Add(1);
        try {
            // Read message - ArgumentData is a projection into our buffer (zero-copy)
            var messageData = array.AsMemory(offset, totalLength - offset);
            var inboundMessage = _readFunc.Invoke(messageData, out var readSize);
            offset += readSize;
            return inboundMessage;
        }
        catch (Exception e) {
            var remaining = array.AsMemory(offset, totalLength - offset);
            _errorLog?.LogError(e, "Couldn't deserialize: {Data}", new TextOrBytes(DataFormat.Bytes, remaining));
            offset = totalLength;
            return null;
        }
    }

    private RpcInboundMessage? TryDeserializeBinaryWithSize(byte[] array, ref int offset, int totalLength)
    {
        _incomingItemCounter?.Add(1);
        var size = 0;
        var isSizeValid = false;
        try {
            size = array.AsSpan(offset).ReadLittleEndian();
            isSizeValid = size > 0 && offset + size <= totalLength;
            if (!isSizeValid)
                throw Errors.InvalidItemSize();

            // Read message - ArgumentData is a projection into our buffer (zero-copy)
            var messageData = array.AsMemory(offset + Int32Size, size - Int32Size);
            var inboundMessage = _readFunc(messageData, out var readSize);
            if (readSize != size - Int32Size)
                throw Errors.InvalidItemSize();

            offset += size;
            return inboundMessage;
        }
        catch (Exception e) {
            var remaining = array.AsMemory(offset, totalLength - offset);
            _errorLog?.LogError(e, "Couldn't deserialize: {Data}", new TextOrBytes(DataFormat.Bytes, remaining));
            offset = isSizeValid ? offset + size : totalLength;
            return null;
        }
    }

    private RpcInboundMessage? TryDeserializeText(byte[] array, ref int offset, int totalLength)
    {
        // Skip delimiter bytes before the next message. A message always starts with its JSON
        // envelope, so LF/RS here is always delimiter residue - and Fusion <= 14.2.39 emitted a
        // delimiter before the frame's first message too, so a frame may well begin with one.
        while (offset < totalLength && array[offset] is LineFeed or RecordSeparator)
            offset++;
        if (offset >= totalLength)
            return null;

        _incomingItemCounter?.Add(1);
        var remaining = array.AsSpan(offset, totalLength - offset);

        // Find Record Separator (RS) - messages are delimited by LF+RS
        var rsIndex = remaining.IndexOf(RecordSeparator);
        // The delimiter's own LF sits right before RS, so the message ends one byte earlier.
        // Including it would append a stray newline to the message's ArgumentData.
        var messageLength = rsIndex < 0
            ? remaining.Length
            : rsIndex - 1;

        try {
            // Pass limited slice to serializer (like master does)
            var messageData = array.AsMemory(offset, messageLength);
            return _readFunc(messageData, out _);
        }
        catch (Exception e) {
            var remainingMemory = array.AsMemory(offset, totalLength - offset);
            _errorLog?.LogError(e, "Couldn't deserialize: {Data}", new TextOrBytes(DataFormat.Text, remainingMemory));
            return null;
        }
        finally {
            // Advance past message and RS delimiter (if present)
            offset = rsIndex < 0
                ? totalLength // Consumed entire buffer
                : offset + rsIndex + 1; // Skip past RS
        }
    }
}
