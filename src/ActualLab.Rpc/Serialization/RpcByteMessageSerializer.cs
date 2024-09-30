using System.Buffers;
using System.Text;
using ActualLab.IO;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization;

public sealed class RpcByteMessageSerializer(RpcPeer peer) : IProjectingByteSerializer<RpcMessage>
{
    [ThreadStatic] private static Encoder? _utf8Encoder;
    [ThreadStatic] private static Decoder? _utf8Decoder;
    [ThreadStatic] private static ArrayPoolBuffer<byte>? _encodeBuffer;
    [ThreadStatic] private static ArrayPoolBuffer<char>? _decodeBuffer;

    public RpcPeer Peer { get; } = peer;
    public bool AllowProjection { get; init; } = true;
    public int MinProjectionSize { get; init; } = 8192;
    public int MaxInefficiencyFactor { get; init; } = 4;
    public int Utf8BufferCapacity { get; init; } = 512; // In characters, used only for header values

    public RpcMessage Read(ReadOnlyMemory<byte> data, out int readLength, out bool isProjection)
        => Read(data, AllowProjection, out readLength, out isProjection);

    public RpcMessage Read(ReadOnlyMemory<byte> data, out int readLength)
        => Read(data, false, out readLength, out _);

    public void Write(IBufferWriter<byte> bufferWriter, RpcMessage value)
    {
        var methodData = value.MethodRef.IdBytes;
        if (methodData.Length > 0xFFFF)
            throw ActualLab.Internal.Errors.Format("Full method name length must not exceed 65535 bytes.");

        var argumentData = value.ArgumentData.Data;
        var requestedLength = 1 + 9 + (2 + methodData.Length) + (4 + argumentData.Length) + 1;

        var writer = new SpanWriter(bufferWriter.GetSpan(requestedLength));

        // CallTypeId
        writer.Remaining[0] = value.CallTypeId;
        writer.Advance(1);

        // RelatedId
        writer.WriteVarULong((ulong)value.RelatedId);

        // MethodRef
        writer.WriteSpanL2(methodData.Span);

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

        var encoder = _utf8Encoder ??= Encoding.UTF8.GetEncoder();
        var encodeBufferCapacity = Utf8BufferCapacity << 1;
        var encodeBuffer = _encodeBuffer ??= new ArrayPoolBuffer<byte>(encodeBufferCapacity);
        try {
            foreach (var h in headers) {
                encodeBuffer.Reset(encodeBufferCapacity);
                var key = h.Key.Utf8NameBytes;
                encoder.Convert(h.Value, encodeBuffer);
                var valueSpan = encodeBuffer.WrittenSpan;

                writer = new SpanWriter(bufferWriter.GetSpan(3 + key.Length + valueSpan.Length));
                writer.WriteSpanL1(key.Span);
                writer.WriteSpanL2(valueSpan);
                bufferWriter.Advance(writer.Offset);
            }
        }
        catch {
            encoder.Reset();
            throw;
        }
    }

    // Private methods

    private RpcMessage Read(ReadOnlyMemory<byte> data, bool allowProjection, out int readLength, out bool isProjection)
    {
        var reader = new MemoryReader(data);

        // CallTypeId
        var callTypeId = reader.Remaining[0];
        reader.Advance(1);

        // RelatedId
        var relatedId = (long)reader.ReadVarULong();

        // Method
        var blob = reader.ReadMemoryL2();
        var methodRef = new RpcMethodRef(blob);
        var methodDef = Peer.ServerMethodResolver[methodRef];
        // We can't have MethodRef bound to blob, coz the blob can be overwritten,
        // so we replace it with either the matching one from registry, or make a blob copy.
        methodRef = methodDef?.Ref ?? new RpcMethodRef(new ByteString(blob.ToArray()), methodRef.HashCode);

        // ArgumentData
        blob = reader.ReadMemoryL4();
        isProjection = allowProjection && blob.Length >= MinProjectionSize && IsProjectable(blob);
        var argumentData = isProjection
            ? new TextOrBytes(DataFormat.Bytes, blob)
            : new TextOrBytes(DataFormat.Bytes, blob.ToArray());

        // Headers
        var headerCount = (int)reader.Remaining[0];
        reader.Advance(1);
        RpcHeader[]? headers = null;
        if (headerCount > 0) {
            headers = new RpcHeader[headerCount];
            var decoder = _utf8Decoder ??= Encoding.UTF8.GetDecoder();
            var decodeBuffer = _decodeBuffer ??= new ArrayPoolBuffer<char>(Utf8BufferCapacity);
            try {
                for (var i = 0; i < headerCount; i++) {
                    decodeBuffer.Reset(Utf8BufferCapacity);

                    // key
                    blob = reader.ReadMemoryL1();
                    var key = new RpcHeaderKey(blob);

                    // h.Value
                    var valueSpan = reader.ReadSpanL2();
                    decoder.Convert(valueSpan, decodeBuffer);
                    headers[i] = new RpcHeader(key, new string(decodeBuffer.WrittenSpan));
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

#pragma warning disable CA1822
    public bool IsProjectable(ReadOnlyMemory<byte> data)
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
