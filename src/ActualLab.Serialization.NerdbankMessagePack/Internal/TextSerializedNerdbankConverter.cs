using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="TextSerialized{T}"/> and its subclasses
/// (notably <see cref="NewtonsoftJsonSerialized{T}"/>). The legacy MessagePack wire format
/// — dictated by <c>[Key(0)] string Data</c> — is a 1-element array <c>[Data]</c> carrying
/// the serialized JSON string. Nerdbank's default reflection provider would otherwise emit
/// <c>{"Data": ...}</c> (map), which breaks both raw byte compatibility AND downstream
/// composites like <see cref="ImmutableOptionSet"/> / <see cref="PropertyBag"/> whose
/// Dictionary-of-NewtonsoftJsonSerialized member carries this shape by reference.
/// </summary>
public class TextSerializedNerdbankConverter<T, TSerialized> : MessagePackConverter<TSerialized?>
    where TSerialized : TextSerialized<T>, new()
{
    public override TSerialized? Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return null;
        var len = reader.ReadArrayHeader();
        if (len != 1)
            throw new MessagePackSerializationException(
                $"Expected 1-element array for {typeof(TSerialized).Name}, got {len}.");
        var data = reader.ReadString() ?? "";
        return new TSerialized { Data = data };
    }

    public override void Write(ref MessagePackWriter writer, in TSerialized? value, SerializationContext context)
    {
        if (value is null) {
            writer.WriteNil();
            return;
        }
        writer.WriteArrayHeader(1);
        writer.Write(value.Data);
    }
}

/// <summary>
/// Closed-over-NewtonsoftJsonSerialized convenience so the converter can be registered as
/// an open generic by the default <see cref="NerdbankMessagePackByteSerializer"/>.
/// </summary>
public sealed class NewtonsoftJsonSerializedNerdbankConverter<T>
    : TextSerializedNerdbankConverter<T, NewtonsoftJsonSerialized<T>>
{ }
