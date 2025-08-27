using System.Buffers;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization;

#pragma warning disable MA0069

public class RpcByteMessageSerializerV3(RpcPeer peer)
    : RpcByteMessageSerializer(peer), IProjectingByteSerializer<RpcMessage>, IRequiresItemSize
{
    // Settings - they affect only performance (i.e., a wire format won't change if you change them)
    public bool AllowProjection { get; init; } = Defaults.AllowProjection;
    public int MinProjectionSize { get; init; } = Defaults.MinProjectionSize;
    public int MaxInefficiencyFactor { get; init; } = Defaults.MaxInefficiencyFactor;

    public virtual RpcMessage Read(ReadOnlyMemory<byte> data, out int readLength, out bool isProjection)
    {
        var reader = new MemoryReader(data);

        // CallTypeId
        var callTypeId = reader.Remaining[0];
        reader.Advance(1);

        // RelatedId
        var relatedId = (long)reader.ReadAltVarULong();

        // MethodRef
        var blob = reader.ReadL2Memory();
        var methodRef = new RpcMethodRef(blob);
        var methodDef = ServerMethodResolver[methodRef];
        // We can't have MethodRef bound to blob, coz the blob can be overwritten,
        // so we replace it with either the matching one from registry or make a blob copy.
        methodRef = methodDef?.Ref ?? new RpcMethodRef(blob.ToArray(), methodRef.HashCode);

        // ArgumentData
        blob = reader.ReadL4Memory();
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

        // CallTypeId
        var callTypeId = reader.Remaining[0];
        reader.Advance(1);

        // RelatedId
        var relatedId = (long)reader.ReadAltVarULong();

        // MethodRef
        var blob = reader.ReadL2Memory();
        var methodRef = new RpcMethodRef(blob);
        var methodDef = ServerMethodResolver[methodRef];
        // We can't have MethodRef bound to blob, coz the blob can be overwritten,
        // so we replace it with either the matching one from registry or make a blob copy.
        methodRef = methodDef?.Ref ?? new RpcMethodRef(blob.ToArray(), methodRef.HashCode);

        // ArgumentData
        blob = reader.ReadL4Memory();
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
        var utf8Name = value.MethodRef.Utf8Name;
        var argumentData = value.ArgumentData;
        var writer = new SpanWriter(bufferWriter.GetSpan(32 + utf8Name.Length + argumentData.Length));

        // CallTypeId
        writer.Remaining[0] = value.CallTypeId;
        writer.Advance(1);

        // RelatedId
        writer.WriteAltVarULong((ulong)value.RelatedId);

        // MethodRef
        writer.WriteL2Span(utf8Name.Span);

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

                writer = new SpanWriter(bufferWriter.GetSpan(8 + key.Length + valueSpan.Length));
                writer.WriteL1Span(key.Span);
                writer.WriteL2Span(valueSpan);
                bufferWriter.Advance(writer.Offset);
                encodeBuffer.Reset();
            }
        }
        catch {
            encoder.Reset();
            throw;
        }
    }

    // Private methods

#pragma warning disable CA1822
    protected bool IsProjectable(ReadOnlyMemory<byte> data)
    {
#if !NETSTANDARD2_0
        return MemoryMarshal.TryGetArray(data, out var segment)
            && segment.Array is { } array
            && array.Length <= MaxInefficiencyFactor * data.Length;
#else
        return false;
#endif
    }
#pragma warning restore CA1822
}
