using System.Buffers;
using Cysharp.Text;

namespace ActualLab.Serialization.Internal;

public class FuncTextSerializer<T>(
    Func<string, T> reader,
    Func<T, string> writer
    ) : ITextSerializer<T>
{
    public bool PreferStringApi => true;
    public Func<string, T> Reader { get; } = reader;
    public Func<T, string> Writer { get; } = writer;

    // Read

    public T Read(string data)
        => Reader.Invoke(data);

    public T Read(in ReadOnlyMemory<byte> data, out int readLength)
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

    public T Read(ReadOnlyMemory<char> data)
    {
#if NETSTANDARD2_0
        return Reader.Invoke(new string(data.ToArray()));
#else
        return Reader.Invoke(new string(data.Span));
#endif
    }

    // Write

    public string Write(T value)
        => Writer.Invoke(value);

    public void Write(IBufferWriter<byte> bufferWriter, T value)
    {
        var result = Writer.Invoke(value);
        var encoder = EncodingExt.Utf8NoBom.GetEncoder();
        encoder.Convert(result.AsSpan(), bufferWriter);
    }

    public void Write(TextWriter textWriter, T value)
    {
        var result = Writer.Invoke(value);
        textWriter.Write(result);
    }
}
