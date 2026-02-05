using System.Buffers;
using ActualLab.Conversion;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// An <see cref="ITextSerializer{T}"/> that converts between <typeparamref name="T"/>
/// and <typeparamref name="TInner"/> using a <see cref="BiConverter{TFrom, TTo}"/>.
/// </summary>
public sealed class ConvertingTextSerializer<T, TInner>(
    ITextSerializer<TInner> serializer,
    BiConverter<T, TInner> converter
    ) : ITextSerializer<T>
{
    public bool PreferStringApi => serializer.PreferStringApi;

    // Read

    public T Read(ReadOnlyMemory<byte> data, out int readLength)
    {
        var inner = serializer.Read(data, out readLength);
        return converter.Backward.Invoke(inner);
    }

    public T Read(string data)
    {
        var inner = serializer.Read(data);
        return converter.Backward.Invoke(inner);
    }

    public T Read(ReadOnlyMemory<char> data)
    {
        var inner = serializer.Read(data);
        return converter.Backward.Invoke(inner);
    }

    // Write

    public void Write(IBufferWriter<byte> bufferWriter, T value)
    {
        var inner = converter.Forward.Invoke(value);
        serializer.Write(bufferWriter, inner);
    }

    public string Write(T value)
    {
        var inner = converter.Forward.Invoke(value);
        return serializer.Write(inner);
    }

    public void Write(TextWriter textWriter, T value)
    {
        var inner = converter.Forward.Invoke(value);
        serializer.Write(textWriter, inner);
    }
}
