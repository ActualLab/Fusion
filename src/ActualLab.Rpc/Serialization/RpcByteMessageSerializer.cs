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

    public static class Defaults
    {
        public static bool AllowProjection { get; set; } = false;
        public static int MinProjectionSize { get; set; } = 8192;
        public static int MaxInefficiencyFactor { get; set; } = 4;
        public static int Utf8BufferCapacity { get; set; } = 512; // In bytes and characters, used only for header values
        public static int Utf8BufferReplaceCapacity { get; set; } = 8192; // In bytes and characters, used only for header values
    }

    // Settings - they affect only on performance (i.e. wire format won't change if you change them)
    public bool AllowProjection { get; init; } = Defaults.AllowProjection;
    public int MinProjectionSize { get; init; } = Defaults.MinProjectionSize;
    public int MaxInefficiencyFactor { get; init; } = Defaults.MaxInefficiencyFactor;
    public int Utf8BufferCapacity { get; init; } = Defaults.Utf8BufferCapacity;
    public int Utf8BufferReplaceCapacity { get; init; } = Defaults.Utf8BufferReplaceCapacity;

    public RpcPeer Peer { get; } = peer;

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
        var encodeBuffer = _encodeBuffer ??= new ArrayPoolBuffer<byte>(Utf8BufferCapacity);
        try {
            foreach (var h in headers) {
                encodeBuffer.Reset(Utf8BufferCapacity, Utf8BufferReplaceCapacity);
                var key = h.Key.Utf8NameBytes;
                encoder.Convert(h.Value.AsSpan(), encodeBuffer);
                var valueSpan = encodeBuffer.WrittenSpan;

                var headerLength = 3 + key.Length + valueSpan.Length;
                writer = new SpanWriter(bufferWriter.GetSpan(headerLength));
                writer.WriteSpanL1(key.Span);
                writer.WriteSpanL2(valueSpan);
                bufferWriter.Advance(headerLength);
            }
        }
        catch {
            encoder.Reset();
            throw;
        }
    }

    // Private methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                    decodeBuffer.Reset(Utf8BufferCapacity, Utf8BufferReplaceCapacity);

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
