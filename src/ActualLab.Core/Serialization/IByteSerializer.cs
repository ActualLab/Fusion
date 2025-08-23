using System.Buffers;

namespace ActualLab.Serialization;

public interface IByteSerializer
{
    public object? Read(in ReadOnlyMemory<byte> data, Type type, out int readLength);
    public void Write(IBufferWriter<byte> bufferWriter, object? value, Type type);

    public IByteSerializer<T> ToTyped<T>(Type? serializedType = null);
}

public interface IByteSerializer<T>
{
    public T Read(in ReadOnlyMemory<byte> data, out int readLength);
    public void Write(IBufferWriter<byte> bufferWriter, T value);
}

/// <summary>
/// A serializer that allows projection of <seealso cref="ReadOnlyMemory{T}"/> parts
/// from source <seealso cref="ReadOnlyMemory{T}"/> on reads.
/// </summary>
/// <typeparam name="T">The serialized type.</typeparam>
public interface IProjectingByteSerializer<T> : IByteSerializer<T>
{
    public bool AllowProjection { get; }

    public T Read(in ReadOnlyMemory<byte> data, out int readLength, out bool isProjection);
}
