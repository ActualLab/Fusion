using System.Buffers;
using ActualLab.Conversion;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// An <see cref="IByteSerializer{T}"/> that converts between <typeparamref name="T"/>
/// and <typeparamref name="TInner"/> using a <see cref="BiConverter{TFrom, TTo}"/>.
/// </summary>
public sealed class ConvertingByteSerializer<T, TInner>(
    IByteSerializer<TInner> serializer,
    BiConverter<T, TInner> converter
    ) : IByteSerializer<T>
{
    public T Read(ReadOnlyMemory<byte> data, out int readLength)
    {
        var inner = serializer.Read(data, out readLength);
        return converter.Backward.Invoke(inner);
    }

    public void Write(IBufferWriter<byte> bufferWriter, T value)
    {
        var inner = converter.Forward.Invoke(value);
        serializer.Write(bufferWriter, inner);
    }
}
