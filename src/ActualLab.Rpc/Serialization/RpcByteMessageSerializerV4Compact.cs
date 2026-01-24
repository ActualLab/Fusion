using ActualLab.Internal;
using ActualLab.IO;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization;

public class RpcByteMessageSerializerV4Compact(RpcPeer peer) : RpcByteMessageSerializerV4(peer)
{
    public override RpcInboundMessage Read(ArrayPoolArrayHandle<byte> buffer, int offset, out int readLength)
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
            headers,
            buffer.NewRef());
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

        // Headers
        if (headers.Length == 0)
            return;

        var encoder = GetUtf8Encoder();
        var encodeBuffer = GetUtf8EncodeBuffer();
        try {
            foreach (var h in headers) {
                var key = h.Key.Utf8Name;
                encoder.Convert(h.Value.AsSpan(), encodeBuffer);
                var valueSpan = encodeBuffer.WrittenSpan;

                writer = new SpanWriter(buffer.GetSpan(8 + key.Length + valueSpan.Length));
                writer.WriteL1Span(key.Span);
                writer.WriteLVarSpan(valueSpan);
                buffer.Advance(writer.Position);
                encodeBuffer.Reset();
            }
        }
        catch {
            encoder.Reset();
            throw;
        }
    }
}
