using ActualLab.Internal;
using ActualLab.IO;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization.Internal;

namespace ActualLab.Rpc.Serialization;

#pragma warning disable MA0069

public sealed class RpcTextMessageSerializerV3(RpcPeer peer) : RpcTextMessageSerializer(peer)
{
    private static readonly byte Delimiter = (byte)'\n';

    public int MaxArgumentDataSize { get; init; } = Defaults.MaxArgumentDataSize;

    public override RpcInboundMessage Read(ArrayPoolArrayHandle<byte> buffer, int offset, out int readLength)
    {
        // Text serializer always copies data, doesn't use zero-copy
        var array = buffer.Array;
        var data = array.AsMemory(offset, buffer.Length - offset);

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
            m.ParseHeaders(),
            default);
    }

    public override void Write(ArrayPoolBuffer<byte> buffer, RpcOutboundMessage message)
    {
        // Serialize arguments if needed
        var argumentData = message.ArgumentData;
        if (argumentData.IsEmpty && message.Arguments is not null && message.ArgumentSerializer is not null) {
            // Set context for types that need it during serialization (e.g., RpcStream)
            var oldContext = RpcOutboundContext.Current;
            RpcOutboundContext.Current = message.Context;
            try {
                var argBuffer = RpcArgumentSerializer.GetWriteBuffer();
                message.ArgumentSerializer.Serialize(message.Arguments, message.NeedsPolymorphism, argBuffer);
                argumentData = RpcArgumentSerializer.GetWriteBufferMemory(argBuffer);
            }
            finally {
                RpcOutboundContext.Current = oldContext;
            }
        }

        if (argumentData.Length > MaxArgumentDataSize)
            throw Errors.SizeLimitExceeded();

        // ArrayPoolBuffer<byte> implements IBufferWriter<byte>
        var writer = new Utf8JsonWriter(buffer);
        JsonSerializer.Serialize(writer, new JsonRpcMessage(message), typeof(JsonRpcMessage), JsonRpcMessageContext.Default);
        writer.Flush();

        // Write delimiter + argumentData
        var span = buffer.GetSpan(1 + argumentData.Length);
        span[0] = Delimiter;
        argumentData.Span.CopyTo(span[1..]);
        buffer.Advance(1 + argumentData.Length);
    }
}
