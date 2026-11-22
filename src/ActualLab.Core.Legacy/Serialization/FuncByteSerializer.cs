using System.Buffers;

namespace ActualLab.Serialization;

/// <summary>
/// An <see cref="IByteSerializer{T}"/> implementation that delegates to read/write functions.
/// </summary>
public class FuncByteSerializer<T>(
    Func<ReadOnlyMemory<byte>, (T Value, int ReadLength)> reader,
    Action<IBufferWriter<byte>, T> writer
    ) : IByteSerializer<T>
{
    public Func<ReadOnlyMemory<byte>, (T Value, int ReadLength)> Reader { get; } = reader;
    public Action<IBufferWriter<byte>, T> Writer { get; } = writer;
    // There is no untyped serializer to expose
    public IByteSerializer Root => ByteSerializer.None;

    public T ReadUnscoped(ReadOnlyMemory<byte> data, out int readLength)
    {
        var result = Reader.Invoke(data);
        readLength = result.ReadLength;
        return result.Value;
    }

    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, T value)
        => Writer.Invoke(bufferWriter, value);
}
