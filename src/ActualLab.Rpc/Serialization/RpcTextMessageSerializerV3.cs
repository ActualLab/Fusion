using ActualLab.Internal;
using ActualLab.IO;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization.Internal;

namespace ActualLab.Rpc.Serialization;

#pragma warning disable MA0069

/// <summary>
/// V3 JSON-based text message serializer that uses <see cref="Internal.JsonRpcMessage"/> for the message envelope.
/// </summary>
public sealed class RpcTextMessageSerializerV3(RpcPeer peer) : RpcTextMessageSerializer(peer)
{
    private static readonly byte Delimiter = (byte)'\n';

    public const int MaxMethodRefSize = RpcMethodRef.MaxUtf8NameLength;
    public const int MaxHeaderCount = 31;
    public const int MaxHeaderKeySize = byte.MaxValue;
    public const int MaxHeaderValueSize = RpcByteMessageSerializer.MaxHeaderSize;
    public const int MaxJsonEncodedByteExpansion = 6;
    public const int MaxEnvelopeSyntaxSize = 259;
    public const int MaxEnvelopeSize = MaxEnvelopeSyntaxSize
        + MaxJsonEncodedByteExpansion
        * (MaxMethodRefSize + MaxHeaderCount * (MaxHeaderKeySize + MaxHeaderValueSize));

    public int MaxArgumentDataSize { get; init; } = Defaults.MaxArgumentDataSize;

    public static int GetMaxMessageSize(int maxArgumentDataSize)
        => checked(MaxEnvelopeSize + 1 + maxArgumentDataSize);

    public override RpcInboundMessage Read(ReadOnlyMemory<byte> data, out int readLength)
    {
        var reader = new Utf8JsonReader(data.Span);
        var m = (JsonRpcMessage)JsonSerializer.Deserialize(ref reader, typeof(JsonRpcMessage), JsonRpcMessageContext.Default)!;
        if (reader.BytesConsumed > MaxEnvelopeSize)
            throw Errors.SizeLimitExceeded();
        ValidateInboundEnvelope(m);
        var methodRef = ServerMethodResolver[m.Method ?? ""]?.Ref ?? new RpcMethodRef(m.Method ?? "");

        var tail = data.Slice((int)reader.BytesConsumed).Span;
        if (tail[0] == Delimiter)
            tail = tail[1..];
        if (tail.Length > MaxArgumentDataSize)
            throw Errors.SizeLimitExceeded();

        var argumentData = (ReadOnlyMemory<byte>)tail.ToArray();
        readLength = data.Length;
        return new RpcInboundMessage(
            m.CallType,
            m.RelatedId,
            methodRef,
            argumentData,
            m.ParseHeaders());
    }

    public override void Write(ArrayPoolBuffer<byte> buffer, RpcOutboundMessage message)
    {
        ValidateOutboundEnvelope(message);
        var envelope = new JsonRpcMessage(message);
        var messageStartOffset = buffer.WrittenCount;

        // ArrayPoolBuffer<byte> implements IBufferWriter<byte>
        var writer = new Utf8JsonWriter(buffer);
        JsonSerializer.Serialize(writer, envelope, typeof(JsonRpcMessage), JsonRpcMessageContext.Default);
        writer.Flush();
        if (buffer.WrittenCount - messageStartOffset > MaxEnvelopeSize) {
            buffer.Position = messageStartOffset;
            throw Errors.SizeLimitExceeded();
        }

        // Write delimiter + arguments (zero-copy into the provided buffer)
        var startOffset = buffer.WrittenCount;
        buffer.GetSpan(1)[0] = Delimiter;
        buffer.Advance(1);
        var argsStartOffset = buffer.WrittenCount;

        var argumentData = message.ArgumentData;
        if (!argumentData.IsEmpty) {
            if (argumentData.Length > MaxArgumentDataSize)
                throw Errors.SizeLimitExceeded();
            var span = buffer.GetSpan(argumentData.Length);
            argumentData.Span.CopyTo(span);
            buffer.Advance(argumentData.Length);
        }
        else {
            // Set context for types that need it during serialization (e.g., RpcStream)
            var oldContext = RpcOutboundContext.Current;
            RpcOutboundContext.Current = message.Context;
            try {
                message.ArgumentSerializer.Serialize(message.Arguments!, message.NeedsPolymorphism, buffer);
            }
            finally {
                RpcOutboundContext.Current = oldContext;
            }
            if (buffer.WrittenCount - argsStartOffset > MaxArgumentDataSize) {
                buffer.Position = startOffset;
                throw Errors.SizeLimitExceeded();
            }
        }
    }

    private static void ValidateInboundEnvelope(JsonRpcMessage message)
    {
        if (IsUtf8SizeExceeded(message.Method ?? "", MaxMethodRefSize))
            throw Errors.SizeLimitExceeded();

        var headers = message.Headers;
        if (headers is null)
            return;
        if ((headers.Count & 1) != 0 || headers.Count > 2 * MaxHeaderCount)
            throw Errors.SizeLimitExceeded();
        for (var i = 0; i < headers.Count; i += 2) {
            if (IsUtf8SizeExceeded(headers[i], MaxHeaderKeySize)
                || IsUtf8SizeExceeded(headers[i + 1], MaxHeaderValueSize))
                throw Errors.SizeLimitExceeded();
        }
    }

    private static void ValidateOutboundEnvelope(RpcOutboundMessage message)
    {
        if (message.MethodDef.Ref.Utf8Name.Length > MaxMethodRefSize)
            throw Errors.SizeLimitExceeded();

        var headers = message.Headers;
        if (headers is null)
            return;
        if (headers.Length > MaxHeaderCount)
            throw Errors.SizeLimitExceeded();
        foreach (var header in headers) {
            if (header.Key.Utf8Name.Length > MaxHeaderKeySize
                || IsUtf8SizeExceeded(header.Value, MaxHeaderValueSize))
                throw Errors.SizeLimitExceeded();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUtf8SizeExceeded(string value, int maxSize)
        => value.Length > maxSize / 3
            && (value.Length > maxSize || EncodingExt.Utf8NoBom.GetByteCount(value) > maxSize);
}
