using System.Diagnostics.Metrics;
using ActualLab.Collections;
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
    private readonly Counter<long>? _incomingItemCounter;
    private readonly Counter<long>? _outgoingItemCounter;
    private readonly ILogger? _errorLog;

    public RpcMessageSerializer Serializer { get; }
    public bool IsTextSerializer { get; }
    public SerializeDelegate Serialize { get; }
    public TryDeserializeDelegate TryDeserialize { get; }

    public RpcFrameCodec(
        RpcMessageSerializer serializer,
        Counter<long>? incomingItemCounter = null,
        Counter<long>? outgoingItemCounter = null,
        ILogger? errorLog = null)
    {
        Serializer = serializer;
        IsTextSerializer = serializer is RpcTextMessageSerializer;

        _readFunc = serializer.ReadFunc;
        _writeFunc = serializer.WriteFunc;
        _incomingItemCounter = incomingItemCounter;
        _outgoingItemCounter = outgoingItemCounter;
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
            // Text format: use LF+RS delimiter between messages (no size prefix)
            if (startOffset != 0) {
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
        _incomingItemCounter?.Add(1);
        var remaining = array.AsSpan(offset, totalLength - offset);

        // Find Record Separator (RS) - messages are delimited by LF+RS
        var rsIndex = remaining.IndexOf(RecordSeparator);
        // Message length: up to RS (or end of buffer), minus trailing LF before RS
        var messageLength = rsIndex < 0
            ? remaining.Length
            : rsIndex; // RS position = message length (LF is at rsIndex-1, so message is [0..rsIndex-1] + LF)

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
