using System.Buffers;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Abstract base class for <see cref="IByteSerializer"/> implementations providing a default typed adapter.
/// </summary>
public abstract class ByteSerializerBase : IByteSerializer
{
    public IByteSerializer Root { get; protected set; }

    // Wrappers pass the root of the serializer they wrap
    protected ByteSerializerBase(IByteSerializer? root = null)
        => Root = root ?? this;

    // Unscoped API

    public abstract object? ReadUnscoped(ReadOnlyMemory<byte> data, Type type, out int readLength);
    public abstract void WriteUnscoped(IBufferWriter<byte> bufferWriter, object? value, Type type);

    // ToTyped

    public virtual IByteSerializer<T> ToTyped<T>(Type? serializedType = null)
        => new CastingByteSerializer<T>(this, serializedType ?? typeof(T));
}
