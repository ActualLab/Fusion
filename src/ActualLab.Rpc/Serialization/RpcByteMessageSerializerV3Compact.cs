using System.Buffers;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization;

public class RpcByteMessageSerializerV3Compact(RpcPeer peer) : RpcByteMessageSerializerV3(peer)
{
    public override RpcMessage Read(ReadOnlyMemory<byte> data, out int readLength, out bool isProjection)
    {
        var reader = new MemoryReader(data);

        // MethodRef
        var hashCode = reader.Remaining.ReadUnchecked<int>();
        var methodDef = ServerMethodResolver[hashCode];
        var methodRef = methodDef?.Ref ?? new RpcMethodRef(default, hashCode);

        // CallTypeId
        var callTypeId = reader.Remaining[4];
        reader.Advance(5);

        // RelatedId
        var relatedId = (long)reader.ReadAltVarULong();

        // ArgumentData
        var blob = reader.ReadL4Memory();
        isProjection = AllowProjection && blob.Length >= MinProjectionSize && IsProjectable(blob);
        var argumentData = isProjection ? blob : (ReadOnlyMemory<byte>)blob.ToArray();

        // Headers
        var headerCount = (int)reader.Remaining[0];
        reader.Advance(1);
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
                    var valueSpan = reader.ReadL2Span();
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

        // MethodRef
        var hashCode = reader.Remaining.ReadUnchecked<int>();
        var methodDef = ServerMethodResolver[hashCode];
        var methodRef = methodDef?.Ref ?? new RpcMethodRef(default, hashCode);

        // CallTypeId
        var callTypeId = reader.Remaining[4];
        reader.Advance(5);

        // RelatedId
        var relatedId = (long)reader.ReadAltVarULong();

        // ArgumentData
        var blob = reader.ReadL4Memory();
        var argumentData = (ReadOnlyMemory<byte>)blob.ToArray();

        // Headers
        var headerCount = (int)reader.Remaining[0];
        reader.Advance(1);
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
                    var valueSpan = reader.ReadL2Span();
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
        var requestedLength = 32 + argumentData.Length;

        var writer = new SpanWriter(bufferWriter.GetSpan(requestedLength));

        // MethodRef
        writer.Remaining.WriteUnchecked(value.MethodRef.HashCode);

        // CallTypeId
        writer.Remaining[4] = value.CallTypeId;
        writer.Advance(5);

        // RelatedId
        writer.WriteAltVarULong((ulong)value.RelatedId);

        // ArgumentData
        writer.WriteL4Span(argumentData.Span);

        // Headers
        var headers = value.Headers ?? RpcHeadersExt.Empty;
        if (headers.Length > 0xFF)
            throw ActualLab.Internal.Errors.Format("Header count must not exceed 255.");

        writer.Remaining[0] = (byte)headers.Length;
        bufferWriter.Advance(writer.Offset + 1);
        if (headers.Length == 0)
            return;

        var encoder = GetUtf8Encoder();
        var encodeBuffer = GetUtf8EncodeBuffer();
        try {
            foreach (var h in headers) {
                var key = h.Key.Utf8Name;
                encoder.Convert(h.Value.AsSpan(), encodeBuffer);
                var valueSpan = encodeBuffer.WrittenSpan;

                var headerLength = 3 + key.Length + valueSpan.Length;
                writer = new SpanWriter(bufferWriter.GetSpan(headerLength));
                writer.WriteL1Span(key.Span);
                writer.WriteL2Span(valueSpan);
                bufferWriter.Advance(headerLength);
                encodeBuffer.Reset();
            }
        }
        catch {
            encoder.Reset();
            throw;
        }
    }
}
