using ActualLab.Internal;
using ActualLab.IO;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization;

public class RpcByteMessageSerializerV4Compact(RpcPeer peer) : RpcByteMessageSerializerV4(peer)
{
    public override RpcInboundMessage Read(ArrayOwner<byte> buffer, int offset, out int readLength)
    {
        var array = buffer.Array;
        var data = array.AsMemory(offset, buffer.Length - offset);
        var reader = new MemoryReader(data);

        // CallTypeId and headerCount
        var headerCountAndCallTypeId = reader.Remaining[0];
        var headerCount = headerCountAndCallTypeId & 0x1F; // 5 lower bits for headerCount
        var callTypeId = (byte)(headerCountAndCallTypeId >> 5); // 3 upper bits for callTypeId

        // RelatedId
        var (relatedId, relatedIdSize) = reader.Remaining.ReadVarUInt64(1);
        reader.Advance(relatedIdSize);

        // MethodRef
        var hashCode = (int)reader.ReadUInt32();
        var methodDef = ServerMethodResolver[hashCode];
        var methodRef = methodDef?.Ref ?? new RpcMethodRef(default, hashCode);

        // ArgumentData - zero-copy projection into the buffer
        var argumentData = reader.ReadLVarMemory(MaxArgumentDataSize);

        // Headers
        RpcHeader[]? headers = null;
        if (headerCount > 0) {
            headers = new RpcHeader[headerCount];
            var decoder = GetUtf8Decoder();
            var decodeBuffer = GetUtf8DecodeBuffer();
            try {
                for (var i = 0; i < headerCount; i++) {
                    var keyBlob = reader.ReadL1Memory();
                    var key = new RpcHeaderKey(keyBlob);
                    var valueSpan = reader.ReadLVarSpan(MaxHeaderSize);
                    decoder.Convert(valueSpan, decodeBuffer);
#if !NETSTANDARD2_0
                    var value = new string(decodeBuffer.WrittenSpan);
#else
                    var value = decodeBuffer.WrittenSpan.ToString();
#endif
                    headers[i] = new RpcHeader(key, value);
                    decodeBuffer.Reset();
                }
            }
            catch {
                decoder.Reset();
                throw;
            }
        }

        readLength = reader.Offset;
        return new RpcInboundMessage(
            callTypeId,
            (long)relatedId,
            methodRef,
            argumentData,
            headers);
    }

    public override void Write(ArrayPoolBuffer<byte> buffer, RpcOutboundMessage message)
    {
        var argumentData = message.ArgumentData;
        if (!argumentData.IsEmpty)
            WriteWithArgumentData(buffer, message, argumentData);

        var startOffset = buffer.WrittenCount;

        // CallTypeId and headerCount
        var headers = message.Headers ?? RpcHeadersExt.Empty;
        if (headers.Length > 31)
            throw Errors.Format("Header count must not exceed 31.");

        var writer = new SpanWriter(buffer.GetSpan(32 + SpanExt.FixedLVarInt32Size));
        writer.Remaining[0] = (byte)(headers.Length | (message.MethodDef.CallType.Id << 5));
        writer.WriteVarUInt64((ulong)message.RelatedId, 1);
        writer.WriteUInt32((uint)message.MethodDef.Ref.HashCode);

        var argumentDataLengthOffset = writer.Position;
        writer.Advance(SpanExt.FixedLVarInt32Size);
        buffer.Advance(writer.Position);

        var argumentDataOffset = buffer.WrittenCount;
        var oldContext = RpcOutboundContext.Current;
        RpcOutboundContext.Current = message.Context;
        try {
            message.ArgumentSerializer.Serialize(message.Arguments!, message.NeedsPolymorphism, buffer);
        }
        finally {
            RpcOutboundContext.Current = oldContext;
        }
        var argumentDataLength = buffer.WrittenCount - argumentDataOffset;
        if (argumentDataLength > MaxArgumentDataSize) {
            buffer.Position = startOffset;
            throw Errors.SizeLimitExceeded();
        }

        buffer.Array.AsSpan(startOffset + argumentDataLengthOffset).WriteFixedLVarInt32(argumentDataLength);
        WriteHeaders(buffer, message);
    }

    private new void WriteWithArgumentData(ArrayPoolBuffer<byte> buffer, RpcOutboundMessage message, ReadOnlyMemory<byte> argumentData)
    {
        if (argumentData.Length > MaxArgumentDataSize)
            throw Errors.SizeLimitExceeded();

        var writer = new SpanWriter(buffer.GetSpan(32 + argumentData.Length));

        // CallTypeId and headerCount
        var headers = message.Headers ?? RpcHeadersExt.Empty;
        if (headers.Length > 31)
            throw Errors.Format("Header count must not exceed 31.");

        writer.Remaining[0] = (byte)(headers.Length | (message.MethodDef.CallType.Id << 5));

        // RelatedId
        writer.WriteVarUInt64((ulong)message.RelatedId, 1);

        // MethodRef
        writer.WriteUInt32((uint)message.MethodDef.Ref.HashCode);

        // ArgumentData
        writer.WriteLVarSpan(argumentData.Span);

        // Commit to buffer
        buffer.Advance(writer.Position);

        WriteHeaders(buffer, message);
    }
}
