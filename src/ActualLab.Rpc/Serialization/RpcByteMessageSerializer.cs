using System.Buffers;
using ActualLab.IO;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization;

#pragma warning disable MA0069

public class RpcByteMessageSerializer(RpcPeer peer)
    : RpcMessageSerializer(peer), IProjectingByteSerializer<RpcMessage>
{
    public static class Defaults
    {
        public static bool AllowProjection { get; set; } = false;
        public static int MinProjectionSize { get; set; } = 8192;
        public static int MaxInefficiencyFactor { get; set; } = 4;
    }

    // Settings - they affect only on performance (i.e. wire format won't change if you change them)
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
        var relatedId = (long)reader.ReadVarULong();

        // MethodRef
        var blob = reader.ReadMemoryL2();
        var methodRef = new RpcMethodRef(blob);
        var methodDef = ServerMethodResolver[methodRef];
        // We can't have MethodRef bound to blob, coz the blob can be overwritten,
        // so we replace it with either the matching one from registry, or make a blob copy.
        methodRef = methodDef?.Ref ?? new RpcMethodRef(blob.ToArray(), methodRef.HashCode);

        // ArgumentData
        blob = reader.ReadMemoryL4();
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
                    blob = reader.ReadMemoryL1();
                    var key = new RpcHeaderKey(blob);

                    // h.Value
                    var valueSpan = reader.ReadSpanL2();
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
        var relatedId = (long)reader.ReadVarULong();

        // MethodRef
        var blob = reader.ReadMemoryL2();
        var methodRef = new RpcMethodRef(blob);
        var methodDef = ServerMethodResolver[methodRef];
        // We can't have MethodRef bound to blob, coz the blob can be overwritten,
        // so we replace it with either the matching one from registry, or make a blob copy.
        methodRef = methodDef?.Ref ?? new RpcMethodRef(blob.ToArray(), methodRef.HashCode);

        // ArgumentData
        blob = reader.ReadMemoryL4();
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
                    blob = reader.ReadMemoryL1();
                    var key = new RpcHeaderKey(blob);

                    // h.Value
                    var valueSpan = reader.ReadSpanL2();
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
        var requestedLength = 1 + 9 + (2 + utf8Name.Length) + (4 + argumentData.Length) + 1;

        var writer = new SpanWriter(bufferWriter.GetSpan(requestedLength));

        // CallTypeId
        writer.Remaining[0] = value.CallTypeId;
        writer.Advance(1);

        // RelatedId
        writer.WriteVarULong((ulong)value.RelatedId);

        // MethodRef
        writer.WriteSpanL2(utf8Name.Span);

        // ArgumentData
        writer.WriteSpanL4(argumentData.Span);

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
                writer.WriteSpanL1(key.Span);
                writer.WriteSpanL2(valueSpan);
                bufferWriter.Advance(headerLength);
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
