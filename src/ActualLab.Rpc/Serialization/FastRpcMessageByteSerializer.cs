using System.Buffers;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization.Internal;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Serialization;

public sealed class FastRpcMessageByteSerializer(IByteSerializer baseSerializer, bool allowProjection = false)
    : IProjectingByteSerializer<RpcMessage>
{
    public bool AllowProjection { get; init; } = allowProjection;
    public int MinProjectionSize { get; init; } = 8192;
    public int MaxInefficiencyFactor { get; init; } = 4;

    public RpcMessage Read(ReadOnlyMemory<byte> data, out int readLength, out bool isProjection)
    {
        var message = baseSerializer.Read<FastRpcMessage>(data, out readLength);
        var span = data.Span[readLength..];
        if (span.Length < 4)
            throw Errors.InvalidSerializedDataFormat();

        var argumentDataLength = span.ReadUnchecked<int>();
        if (argumentDataLength < 4)
            throw Errors.InvalidSerializedDataFormat();
        if (span.Length < argumentDataLength)
            throw Errors.InvalidSerializedDataFormat();

        var argumentDataStart = readLength + 4;
        readLength += argumentDataLength;

        var argumentData = data[argumentDataStart..readLength];
        isProjection = AllowProjection && argumentData.Length >= MinProjectionSize && IsProjectable(argumentData);
        return message.ToRpcMessage(isProjection
            ? new TextOrBytes(DataFormat.Bytes, argumentData)
            : new TextOrBytes(DataFormat.Bytes, argumentData.ToArray()));
    }

    public RpcMessage Read(ReadOnlyMemory<byte> data, out int readLength)
    {
        var message = baseSerializer.Read<FastRpcMessage>(data, out readLength);
        var span = data.Span[readLength..];
        if (span.Length < 4)
            throw Errors.InvalidSerializedDataFormat();

        var argumentDataLength = span.ReadUnchecked<int>();
        if (span.Length < argumentDataLength)
            throw Errors.InvalidSerializedDataFormat();

        var argumentDataStart = readLength + 4;
        readLength += argumentDataLength;
        var argumentData = data[argumentDataStart..readLength].ToArray();
        return message.ToRpcMessage(new TextOrBytes(DataFormat.Bytes, argumentData));
    }

    public void Write(IBufferWriter<byte> bufferWriter, RpcMessage value)
    {
        baseSerializer.Write(bufferWriter, new FastRpcMessage(value));
        var argumentData = value.ArgumentData.Data;
        var argumentDataLength = argumentData.Length + 4;
        var bufferSpan = bufferWriter.GetSpan(argumentDataLength);
        bufferSpan.WriteUnchecked(argumentDataLength);
        argumentData.Span.CopyTo(bufferSpan[4..]);
        bufferWriter.Advance(argumentDataLength);
    }

    // Private methods

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
