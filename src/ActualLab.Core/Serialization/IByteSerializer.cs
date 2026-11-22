using System.Buffers;

namespace ActualLab.Serialization;

/// <summary>
/// Defines a contract for binary serialization and deserialization of objects.
/// </summary>
public interface IByteSerializer
{
    // The serializer exposed as ByteSerializer.Current while this one runs: typed serializers and wrappers
    // expose the untyped serializer converters of nested values (e.g. UniSerialized<T>) expect
    public IByteSerializer Root { get; }

    // Read/WriteUnscoped don't activate ByteSerializer.Current: the Read/Write extension methods do,
    // and code making a series of calls wraps them into a single new ByteSerializer.Scope(serializer.Root)
    public object? ReadUnscoped(ReadOnlyMemory<byte> data, Type type, out int readLength);
    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, object? value, Type type);

    public IByteSerializer<T> ToTyped<T>(Type? serializedType = null);
}

/// <summary>
/// Defines a contract for binary serialization and deserialization of <typeparamref name="T"/>.
/// </summary>
public interface IByteSerializer<T>
{
    // See IByteSerializer.Root and Read/WriteUnscoped
    public IByteSerializer Root { get; }

    public T ReadUnscoped(ReadOnlyMemory<byte> data, out int readLength);
    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, T value);
}

/// <summary>
/// A serializer that allows projection of <seealso cref="ReadOnlyMemory{T}"/> parts
/// from source <seealso cref="ReadOnlyMemory{T}"/> on reads.
/// </summary>
/// <typeparam name="T">The serialized type.</typeparam>
public interface IProjectingByteSerializer<T> : IByteSerializer<T>
{
    public bool AllowProjection { get; }

    public T Read(ReadOnlyMemory<byte> data, out int readLength, out bool isProjection);
}
