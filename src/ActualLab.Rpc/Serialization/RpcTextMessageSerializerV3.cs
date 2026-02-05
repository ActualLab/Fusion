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

    public int MaxArgumentDataSize { get; init; } = Defaults.MaxArgumentDataSize;

    public override RpcInboundMessage Read(ReadOnlyMemory<byte> data, out int readLength)
    {

        var reader = new Utf8JsonReader(data.Span);
        var m = (JsonRpcMessage)JsonSerializer.Deserialize(ref reader, typeof(JsonRpcMessage), JsonRpcMessageContext.Default)!;
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
        // ArrayPoolBuffer<byte> implements IBufferWriter<byte>
        var writer = new Utf8JsonWriter(buffer);
        JsonSerializer.Serialize(writer, new JsonRpcMessage(message), typeof(JsonRpcMessage), JsonRpcMessageContext.Default);
        writer.Flush();

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
}
