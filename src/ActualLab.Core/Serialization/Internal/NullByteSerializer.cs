using System.Buffers;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Ignores any operation.
/// </summary>
public sealed class NullByteSerializer : IByteSerializer
{
    public static readonly NullByteSerializer Instance = new();

    public IByteSerializer Root => this;

    public object? ReadUnscoped(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        readLength = 0;
        return null;
    }

    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, object? value, Type type)
    { }

    public IByteSerializer<T> ToTyped<T>(Type? serializedType = null)
        => NullByteSerializer<T>.Instance;
}

/// <summary>
/// Ignores any operation.
/// </summary>
public sealed class NullByteSerializer<T> : IByteSerializer<T>
{
    public static readonly NullByteSerializer<T> Instance = new();

    public IByteSerializer Root => NullByteSerializer.Instance;

    public T ReadUnscoped(ReadOnlyMemory<byte> data, out int readLength)
    {
        readLength = 0;
        return default!;
    }

    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, T value)
    { }
}
