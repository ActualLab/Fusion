using System.Buffers;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// A typed <see cref="IByteSerializer{T}"/> adapter that delegates to an untyped <see cref="IByteSerializer"/>.
/// </summary>
public sealed class CastingByteSerializer<T>(
    IByteSerializer untypedSerializer,
    Type serializedType
    ) : IByteSerializer<T>
{
    public IByteSerializer UntypedSerializer { get; } = untypedSerializer;
    public Type SerializedType { get; } = serializedType;

    public T Read(ReadOnlyMemory<byte> data, out int readLength)
        => (T) UntypedSerializer.Read(data, SerializedType, out readLength)!;

    public void Write(IBufferWriter<byte> bufferWriter, T value)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => UntypedSerializer.Write(bufferWriter, value, SerializedType);
}
