using System.Buffers;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Infrastructure;
using CommunityToolkit.HighPerformance;

namespace ActualLab.Rpc.Serialization;

public sealed class RpcByteMessageSerializer(RpcPeer peer) : IProjectingByteSerializer<RpcMessage>
{
    public RpcPeer Peer { get; } = peer;
    public bool AllowProjection { get; init; } = true;
    public int MinProjectionSize { get; init; } = 8192;
    public int MaxInefficiencyFactor { get; init; } = 4;

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
        var headers = value.Headers ?? RpcHeadersExt.Empty;
        if (headers.Length > 0xFF)
            throw ActualLab.Internal.Errors.Format("Header count must not exceed 255.");

        var requestedLength = 1 + 9 + (2 + methodData.Length) + (4 + argumentData.Length) + 1;
        var originalSpan = bufferWriter.GetSpan(requestedLength);

        // CallTypeId
        originalSpan[0] = value.CallTypeId;
        var span = originalSpan[1..];

        // RelatedId
        WriteRelatedId(ref span, (ulong)value.RelatedId);

        // MethodRef
        span.WriteUnchecked((ushort)methodData.Length);
        span = span[2..];
        methodData.Span.CopyTo(span);
        span = span[methodData.Length..];

        // ArgumentData
        span.WriteUnchecked(argumentData.Length);
        span = span[4..];
        argumentData.Span.CopyTo(span);
        span = span[argumentData.Length..];

        // Headers
        span[0] = (byte)headers.Length;
        bufferWriter.Advance(originalSpan.Length - span.Length + 1);
        if (headers.Length != 0) {
            foreach (var h in headers) {
                var k = h.Key.Utf8NameBytes;
                if (k.Length > 0xFF)
                    throw ActualLab.Internal.Errors.Format("Header key length must not exceed 255 bytes.");

                var v = h.Value;
                if (v.Length > 0x7FFF)
                    throw ActualLab.Internal.Errors.Format("Header value length must not exceed 32767 characters.");

                var headerEnd = 3 + k.Length + (v.Length << 1);
                span = bufferWriter.GetSpan(headerEnd);

                // key
                span[0] = (byte)k.Length;
                k.Span.CopyTo(span[1..]);
                span = span[(1 + k.Length)..];

                // value
                span.WriteUnchecked((ushort)v.Length);
                v.AsSpan().Cast<char, byte>().CopyTo(span[2..]);
                bufferWriter.Advance(headerEnd);
            }
        }
    }

    // Private methods

    private RpcMessage Read(ReadOnlyMemory<byte> data, bool allowProjection, out int readLength, out bool isProjection)
    {
        var dataSpan = data.Span;
        var span = dataSpan;

        // CallTypeId
        var callTypeId = span[0];
        span = span[1..];

        // RelatedId
        var relatedId = (long)ReadRelatedId(ref span);

        // Method
        var blobStart = data.Length - span.Length + 2;
        var blobEnd = blobStart + span.ReadUnchecked<ushort>();
        var blob = data[blobStart..blobEnd];
        var methodRef = new RpcMethodRef(blob);
        var methodDef = Peer.ServerMethodResolver[methodRef];
        // We can't have MethodRef bound to blob, coz the blob can be overwritten,
        // so we replace it with either the matching one from registry, or make a blob copy.
        methodRef = methodDef?.Ref ?? new RpcMethodRef(new ByteString(blob.ToArray()), methodRef.HashCode);
        span = dataSpan[blobEnd..];

        // ArgumentData
        blobStart = blobEnd + 4;
        blobEnd = blobStart + span.ReadUnchecked<int>();
        blob = data[blobStart..blobEnd];
        isProjection = allowProjection && blob.Length >= MinProjectionSize && IsProjectable(blob);
        var argumentData = isProjection
            ? new TextOrBytes(DataFormat.Bytes, blob)
            : new TextOrBytes(DataFormat.Bytes, blob.ToArray());

        // Headers
        var headerCount = (int)dataSpan[blobEnd];
        var headerStart = blobEnd + 1;
        RpcHeader[]? headers = null;
        if (headerCount > 0) {
            headers = new RpcHeader[headerCount];
            for (var i = 0; i < headerCount; i++) {
                // key
                blobStart = headerStart + 1;
                blobEnd = blobStart + dataSpan.ReadUnchecked<byte>(headerStart);
                blob = data[blobStart..blobEnd];
                var key = new RpcHeaderKey(blob);

                // h.Value
                blobStart = blobEnd + 2;
                blobEnd = blobStart + (dataSpan.ReadUnchecked<ushort>(blobEnd) << 1);
                var v = new string(dataSpan[blobStart..blobEnd].Cast<byte, char>());
                headers[i] = new RpcHeader(key, v);
                headerStart = blobEnd;
            }
        }

        readLength = headerStart;
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

    // Private methods

    private static void WriteRelatedId(ref Span<byte> span, ulong relatedId)
    {
        if (relatedId <= 0xFFFF) {
            span.WriteUnchecked(2);
            span.WriteUnchecked(1, (ushort)relatedId);
            span = span[3..];
        }
        else if (relatedId <= 0xFFFFFFFF) {
            span.WriteUnchecked(4);
            span.WriteUnchecked(1, (uint)relatedId);
            span = span[5..];
        }
        else {
            span.WriteUnchecked(8);
            span.WriteUnchecked(1, relatedId);
            span = span[9..];
        }
    }

    private static ulong ReadRelatedId(ref ReadOnlySpan<byte> span)
    {
        var size = span[0];
        var result = size switch {
            2 => span.ReadUnchecked<ushort>(1),
            4 => span.ReadUnchecked<uint>(1),
            8 => span.ReadUnchecked<ulong>(1),
            _ => throw ActualLab.Internal.Errors.Format("Invalid message format."),
        };
        span = span[(size + 1)..];
        return result;
    }
}
