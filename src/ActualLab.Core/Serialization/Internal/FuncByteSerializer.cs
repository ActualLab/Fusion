using System.Buffers;

namespace ActualLab.Serialization.Internal;

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

    public T Read(ReadOnlyMemory<byte> data, out int readLength)
    {
        var result = Reader.Invoke(data);
        readLength = result.ReadLength;
        return result.Value;
    }

    public void Write(IBufferWriter<byte> bufferWriter, T value)
        => Writer.Invoke(bufferWriter, value);
}
