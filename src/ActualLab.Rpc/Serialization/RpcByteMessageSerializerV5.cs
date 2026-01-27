using ActualLab.Internal;
using ActualLab.IO;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Infrastructure;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc.Serialization;

#pragma warning disable MA0069

public class RpcByteMessageSerializerV5(RpcPeer peer) : RpcByteMessageSerializer(peer)
{
    public int MaxArgumentDataSize { get; init; } = Defaults.MaxArgumentDataSize;

    public override RpcInboundMessage Read(ReadOnlyMemory<byte> data, out int readLength)
    {
        var reader = new MemoryReader(data);

        // CallTypeId and headerCount
        var headerCountAndCallTypeId = reader.Remaining[0];
        var headerCount = headerCountAndCallTypeId & 0x1F; // 5 lower bits for headerCount
        var callTypeId = (byte)(headerCountAndCallTypeId >> 5); // 3 upper bits for callTypeId

        // RelatedId
        var (relatedId, relatedIdSize) = reader.Remaining.ReadVarUInt64(1);
        reader.Advance(relatedIdSize);

        // MethodRef
        var blob = reader.ReadLVarMemory(MaxMethodRefSize);
        var methodRef = new RpcMethodRef(blob);
        var methodDef = ServerMethodResolver[methodRef];
        methodRef = methodDef?.Ref ?? new RpcMethodRef(blob.ToArray(), methodRef.HashCode);

        // ArgumentData - zero-copy projection into the buffer (fixed 4-byte size prefix)
        var argumentData = reader.ReadL4Memory(MaxArgumentDataSize);

        // Headers
        RpcHeader[]? headers = null;
        if (headerCount > 0) {
            headers = new RpcHeader[headerCount];
            var decoder = GetUtf8Decoder();
            var decodeBuffer = GetUtf8DecodeBuffer();
            try {
                for (var i = 0; i < headerCount; i++) {
                    blob = reader.ReadL1Memory();
                    var key = new RpcHeaderKey(blob);
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
        return new RpcInboundMessage(callTypeId, (long)relatedId, methodRef, argumentData, headers);
    }

    public override void Write(ArrayPoolBuffer<byte> buffer, RpcOutboundMessage message)
    {
        var argumentData = message.ArgumentData;
        if (!argumentData.IsEmpty) {
            WriteWithArgumentData(buffer, message, argumentData);
            return;
        }

        var startOffset = buffer.WrittenCount;
        var utf8Name = message.MethodDef.Ref.Utf8Name;

        // CallTypeId and headerCount
        var headers = message.Headers ?? RpcHeadersExt.Empty;
        if (headers.Length > 31)
            throw Errors.Format("Header count must not exceed 31.");

        // Header, RelatedId, MethodRef, fixed 4-byte ArgumentData length prefix
        var writer = new SpanWriter(buffer.GetSpan(32 + utf8Name.Length + 4));
        writer.Remaining[0] = (byte)(headers.Length | (message.MethodDef.CallType.Id << 5));
        writer.WriteVarUInt64((ulong)message.RelatedId, 1);
        writer.WriteLVarSpan(utf8Name.Span);

        var argumentDataLengthOffset = writer.Position;
        writer.Advance(4);
        buffer.Advance(writer.Position);

        // Serialize args directly into the provided buffer (no extra buffer)
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

        // Backfill fixed 4-byte length prefix
        buffer.Array.AsSpan(startOffset + argumentDataLengthOffset).WriteUnchecked(argumentDataLength);

        WriteHeaders(buffer, message);
    }

    protected void WriteWithArgumentData(ArrayPoolBuffer<byte> buffer, RpcOutboundMessage message, ReadOnlyMemory<byte> argumentData)
    {
        var utf8Name = message.MethodDef.Ref.Utf8Name;
        if (argumentData.Length > MaxArgumentDataSize)
            throw Errors.SizeLimitExceeded();

        var writer = new SpanWriter(buffer.GetSpan(32 + utf8Name.Length + 4 + argumentData.Length));

        // CallTypeId and headerCount
        var headers = message.Headers ?? RpcHeadersExt.Empty;
        if (headers.Length > 31)
            throw Errors.Format("Header count must not exceed 31.");

        writer.Remaining[0] = (byte)(headers.Length | (message.MethodDef.CallType.Id << 5));

        // RelatedId
        writer.WriteVarUInt64((ulong)message.RelatedId, 1);

        // MethodRef
        writer.WriteLVarSpan(utf8Name.Span);

        // ArgumentData (fixed 4-byte size prefix)
        writer.WriteL4Span(argumentData.Span);

        // Commit to buffer
        buffer.Advance(writer.Position);

        WriteHeaders(buffer, message);
    }

    protected static void WriteHeaders(ArrayPoolBuffer<byte> buffer, RpcOutboundMessage message)
    {
        var headers = message.Headers ?? RpcHeadersExt.Empty;
        if (headers.Length == 0)
            return;

        var encoder = GetUtf8Encoder();
        var encodeBuffer = GetUtf8EncodeBuffer();
        try {
            foreach (var h in headers) {
                var key = h.Key.Utf8Name;
                encoder.Convert(h.Value.AsSpan(), encodeBuffer);
                var valueSpan = encodeBuffer.WrittenSpan;

                var writer = new SpanWriter(buffer.GetSpan(8 + key.Length + valueSpan.Length));
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
