using System.Buffers;
using ActualLab.Internal;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization;

public class RpcByteMessageSerializerV4Compact(RpcPeer peer) : RpcByteMessageSerializerV4(peer)
{
    public override RpcMessage Read(ReadOnlyMemory<byte> data, out int readLength, out bool isProjection)
    {
        var reader = new MemoryReader(data);

        // CallTypeId and headerCount
        var headerCountAndCallTypeId = reader.Remaining[0];
        var headerCount = headerCountAndCallTypeId & 0x1F; // 5 lower bits for headerCount
        var callTypeId = (byte)(headerCountAndCallTypeId >> 5); // 3 upper bits for callTypeId
        reader.Advance(1);

        // RelatedId
        var relatedId = (long)reader.ReadVarULong();

        // MethodRef
        var hashCode = (int)reader.ReadUInt();
        var methodDef = ServerMethodResolver[hashCode];
        var methodRef = methodDef?.Ref ?? new RpcMethodRef(default, hashCode);

        // ArgumentData
        var blob = reader.ReadLVarMemory();
        isProjection = AllowProjection && blob.Length >= MinProjectionSize && IsProjectable(blob);
        var argumentData = isProjection ? blob : (ReadOnlyMemory<byte>)blob.ToArray();

        // Headers
        RpcHeader[]? headers = null;
        if (headerCount > 0) {
            headers = new RpcHeader[headerCount];
            var decoder = GetUtf8Decoder();
            var decodeBuffer = GetUtf8DecodeBuffer();
            try {
                for (var i = 0; i < headerCount; i++) {
                    // key
                    blob = reader.ReadL1Memory();
                    var key = new RpcHeaderKey(blob);

                    // h.Value
                    var valueSpan = reader.ReadLVarSpan();
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
        return new RpcMessage(callTypeId, relatedId, methodRef, argumentData, headers);
    }

    public override RpcMessage Read(ReadOnlyMemory<byte> data, out int readLength)
    {
        var reader = new MemoryReader(data);

        // CallTypeId and headerCount
        var headerCountAndCallTypeId = reader.Remaining[0];
        var headerCount = headerCountAndCallTypeId & 0x1F; // 5 lower bits for headerCount
        var callTypeId = (byte)(headerCountAndCallTypeId >> 5); // 3 upper bits for callTypeId
        reader.Advance(1);

        // RelatedId
        var relatedId = (long)reader.ReadVarULong();

        // MethodRef
        var hashCode = (int)reader.ReadUInt();
        var methodDef = ServerMethodResolver[hashCode];
        var methodRef = methodDef?.Ref ?? new RpcMethodRef(default, hashCode);

        // ArgumentData
        var blob = reader.ReadLVarMemory();
        var argumentData = (ReadOnlyMemory<byte>)blob.ToArray();

        // Headers
        RpcHeader[]? headers = null;
        if (headerCount > 0) {
            headers = new RpcHeader[headerCount];
            var decoder = GetUtf8Decoder();
            var decodeBuffer = GetUtf8DecodeBuffer();
            try {
                for (var i = 0; i < headerCount; i++) {
                    // key
                    blob = reader.ReadL1Memory();
                    var key = new RpcHeaderKey(blob);

                    // h.Value
                    var valueSpan = reader.ReadLVarSpan();
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
        return new RpcMessage(callTypeId, relatedId, methodRef, argumentData, headers);
    }

    public override void Write(IBufferWriter<byte> bufferWriter, RpcMessage value)
    {
        var argumentData = value.ArgumentData;
        var writer = new SpanWriter(bufferWriter.GetSpan(32 + argumentData.Length));

        // CallTypeId and headerCount
        var headers = value.Headers ?? RpcHeadersExt.Empty;
        if (headers.Length > 31)
            throw Errors.Format("Header count must not exceed 31.");

        writer.Remaining[0] = (byte)(headers.Length | (value.CallTypeId << 5));
        writer.Advance(1);

        // RelatedId
        writer.WriteVarULong((ulong)value.RelatedId);

        // MethodRef
        writer.WriteUInt((uint)value.MethodRef.HashCode);

        // ArgumentData
        writer.WriteLVarSpan(argumentData.Span);

        // Commit to bufferWriter
        bufferWriter.Advance(writer.Offset);

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

                writer = new SpanWriter(bufferWriter.GetSpan(8 + key.Length + valueSpan.Length));
                writer.WriteL1Span(key.Span);
                writer.WriteLVarSpan(valueSpan);
                bufferWriter.Advance(writer.Offset);
                encodeBuffer.Reset();
            }
        }
        catch {
            encoder.Reset();
            throw;
        }
    }
}
