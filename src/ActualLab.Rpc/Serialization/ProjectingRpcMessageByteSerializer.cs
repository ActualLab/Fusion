using System.Buffers;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization.Internal;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Serialization;

public class ProjectingRpcMessageByteSerializer(IByteSerializer serializer, int maxInefficiencyFactor = 4)
    : IProjectingByteSerializer<RpcMessage>
{
    public readonly IByteSerializer Serializer = serializer;
    public readonly int MaxInefficiencyFactor = maxInefficiencyFactor;

    public RpcMessage Read(ReadOnlyMemory<byte> data, out int readLength, out bool isProjection)
    {
        var message = Serializer.Read<FastRpcMessage>(data, out readLength);
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
        if (argumentDataLength == 4) {
            isProjection = false;
            return message.ToRpcMessage(TextOrBytes.EmptyBytes);
        }

        var argumentData = data[argumentDataStart..readLength];
        isProjection = CanUseProjection(argumentData);
        return message.ToRpcMessage(isProjection
            ? new TextOrBytes(DataFormat.Bytes, argumentData)
            : new TextOrBytes(DataFormat.Bytes, argumentData.ToArray()));
    }

    public RpcMessage Read(ReadOnlyMemory<byte> data, out int readLength)
    {
        var message = Serializer.Read<FastRpcMessage>(data, out readLength);
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
        Serializer.Write(bufferWriter, new FastRpcMessage(value));
        var argumentData = value.ArgumentData.Data;
        var argumentDataLength = argumentData.Length + 4;
        var span = bufferWriter.GetSpan(argumentDataLength);
        span.WriteUnchecked(argumentDataLength);
        argumentData.Span.CopyTo(span[4..]);
        bufferWriter.Advance(argumentDataLength);
    }

    // Private methods

    public bool CanUseProjection(ReadOnlyMemory<byte> data)
    {
#if !NETSTANDARD2_0
        return MemoryMarshal.TryGetArray(data, out var segment)
            && segment.Array is { } array
            && array.Length <= MaxInefficiencyFactor * data.Length;
#else
        return false;
#endif
    }
}
