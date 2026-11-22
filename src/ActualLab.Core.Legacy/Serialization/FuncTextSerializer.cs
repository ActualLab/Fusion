using System.Buffers;
using Cysharp.Text;

namespace ActualLab.Serialization;

/// <summary>
/// An <see cref="ITextSerializer{T}"/> implementation that delegates to read/write functions.
/// </summary>
public class FuncTextSerializer<T>(
    Func<string, T> reader,
    Func<T, string> writer
    ) : ITextSerializer<T>
{
    public bool PreferStringApi => true;
    public Func<string, T> Reader { get; } = reader;
    public Func<T, string> Writer { get; } = writer;
    // There is no untyped serializer to expose
    public ITextSerializer Root => TextSerializer.None;
    IByteSerializer IByteSerializer<T>.Root => Root;

    // Read

    public T ReadUnscoped(string data)
        => Reader.Invoke(data);

    public T ReadUnscoped(ReadOnlyMemory<byte> data, out int readLength)
    {
        var decoder = EncodingExt.Utf8NoBom.GetDecoder();
        var buffer = ZString.CreateStringBuilder(); // Fine here: it is used zero-alloc IBufferWriter<char>
        try {
            decoder.Convert(data.Span, ref buffer);
            readLength = data.Length;
            return Reader.Invoke(buffer.ToString());
        }
        finally {
            buffer.Dispose();
        }
    }

    public T ReadUnscoped(ReadOnlyMemory<char> data)
    {
#if NETSTANDARD2_0
        return Reader.Invoke(new string(data.ToArray()));
#else
        return Reader.Invoke(new string(data.Span));
#endif
    }

    // Write

    public string WriteUnscoped(T value)
        => Writer.Invoke(value);

    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, T value)
    {
        var result = Writer.Invoke(value);
        var encoder = EncodingExt.Utf8NoBom.GetEncoder();
        encoder.Convert(result.AsSpan(), bufferWriter);
    }

    public void WriteUnscoped(TextWriter textWriter, T value)
    {
        var result = Writer.Invoke(value);
        textWriter.Write(result);
    }
}
