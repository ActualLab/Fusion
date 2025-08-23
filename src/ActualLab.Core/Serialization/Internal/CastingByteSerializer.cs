using System.Buffers;

namespace ActualLab.Serialization.Internal;

public sealed class CastingByteSerializer<T>(
    IByteSerializer untypedSerializer,
    Type serializedType
    ) : IByteSerializer<T>
{
    public IByteSerializer UntypedSerializer { get; } = untypedSerializer;
    public Type SerializedType { get; } = serializedType;

    public T Read(in ReadOnlyMemory<byte> data, out int readLength)
        => (T) UntypedSerializer.Read(data, SerializedType, out readLength)!;

    public void Write(IBufferWriter<byte> bufferWriter, T value)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => UntypedSerializer.Write(bufferWriter, value, SerializedType);
}
