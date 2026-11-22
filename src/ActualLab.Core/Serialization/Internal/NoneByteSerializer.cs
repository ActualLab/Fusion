using System.Buffers;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Throws an error on any operation.
/// </summary>
public sealed class NoneByteSerializer : IByteSerializer
{
    public static readonly NoneByteSerializer Instance = new();

    public IByteSerializer Root => this;

    public object? ReadUnscoped(ReadOnlyMemory<byte> data, Type type, out int readLength)
        => throw Errors.NoSerializer();

    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, object? value, Type type)
        => throw Errors.NoSerializer();

    public IByteSerializer<T> ToTyped<T>(Type? serializedType = null)
        => NoneByteSerializer<T>.Instance;
}

/// <summary>
/// Throws an error on any operation.
/// </summary>
public sealed class NoneByteSerializer<T> : IByteSerializer<T>
{
    public static readonly NoneByteSerializer<T> Instance = new();

    public IByteSerializer Root => NoneByteSerializer.Instance;

    public T ReadUnscoped(ReadOnlyMemory<byte> data, out int readLength)
        => throw Errors.NoSerializer();

    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, T value)
        => throw Errors.NoSerializer();
}
