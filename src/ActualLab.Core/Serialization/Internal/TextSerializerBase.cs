using System.Buffers;
using Cysharp.Text;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Abstract base class for <see cref="ITextSerializer"/> implementations providing default byte/char conversions.
/// </summary>
public abstract class TextSerializerBase : ITextSerializer
{
    public ITextSerializer Root { get; protected set; }
    IByteSerializer IByteSerializer.Root => Root;
    public bool PreferStringApi { get; protected init; } = true;

    // Wrappers pass the root of the serializer they wrap
    protected TextSerializerBase(ITextSerializer? root = null)
        => Root = root ?? this;

    // Unscoped API

    public abstract object? ReadUnscoped(string data, Type type);
    public abstract string WriteUnscoped(object? value, Type type);

    public virtual object? ReadUnscoped(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        var decoder = EncodingExt.Utf8NoBom.GetDecoder();
        var buffer = ZString.CreateStringBuilder(); // Fine here: it is used zero-alloc IBufferWriter<char>
        try {
            decoder.Convert(data.Span, ref buffer);
            readLength = data.Length;
            return ReadUnscoped(buffer.ToString(), type);
        }
        finally {
            buffer.Dispose();
        }
    }

    public virtual object? ReadUnscoped(ReadOnlyMemory<char> data, Type type)
    {
#if NETSTANDARD2_0
        return ReadUnscoped(new string(data.ToArray()), type);
#else
        return ReadUnscoped(new string(data.Span), type);
#endif
    }

    public virtual void WriteUnscoped(IBufferWriter<byte> bufferWriter, object? value, Type type)
    {
        var result = WriteUnscoped(value, type);
        var encoder = EncodingExt.Utf8NoBom.GetEncoder();
        encoder.Convert(result.AsSpan(), bufferWriter);
    }

    public virtual void WriteUnscoped(TextWriter textWriter, object? value, Type type)
    {
        var result = WriteUnscoped(value, type);
        textWriter.Write(result);
    }

    // ToTyped

    IByteSerializer<T> IByteSerializer.ToTyped<T>(Type? serializedType)
        => ToTyped<T>(serializedType);

    public virtual ITextSerializer<T> ToTyped<T>(Type? serializedType = null)
        => new CastingTextSerializer<T>(this, serializedType ?? typeof(T));
}
