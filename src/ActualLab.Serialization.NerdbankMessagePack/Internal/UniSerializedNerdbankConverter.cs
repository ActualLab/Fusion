using System.Buffers;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="UniSerialized{T}"/>: a 1-element array whose single element
/// is a <c>bin</c> holding the value, i.e. the same layout <see cref="UniSerializedMessagePackFormatter{T}"/> uses.
/// </summary>
public sealed class UniSerializedNerdbankConverter<T> : MessagePackConverter<UniSerialized<T>>
{
    public override UniSerialized<T> Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return default;

        var len = reader.ReadArrayHeader();
        if (len != 1)
            throw new MessagePackSerializationException(
                $"Expected 1-element array for UniSerialized<{typeof(T).Name}>, got {len}.");
        var raw = reader.ReadBytes();
        if (!raw.HasValue || raw.Value.Length == 0)
            return default;

        var innerReader = new MessagePackReader(raw.Value);
        var valueConverter = context.GetConverter<T>(context.TypeShapeProvider);
        return new UniSerialized<T> { Value = valueConverter.Read(ref innerReader, context)! };
    }

    public override void Write(ref MessagePackWriter writer, in UniSerialized<T> value, SerializationContext context)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var innerWriter = new MessagePackWriter(buffer);
        var valueConverter = context.GetConverter<T>(context.TypeShapeProvider);
        valueConverter.Write(ref innerWriter, value.Value, context);
        innerWriter.Flush();
        writer.WriteArrayHeader(1);
        writer.Write(buffer.WrittenSpan);
    }
}
