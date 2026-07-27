using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// A MessagePack formatter for <see cref="TypeDecoratingUniSerialized{TSchema,T}"/>.
/// </summary>
public sealed class TypeDecoratingUniSerializedMessagePackFormatter<TSchema, T>
    : IMessagePackFormatter<TypeDecoratingUniSerialized<TSchema, T>>
    where TSchema : TypeSchema, new()
{
    public void Serialize(
        ref MessagePackWriter writer,
        TypeDecoratingUniSerialized<TSchema, T> value,
        MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(1);
        options.Resolver.GetFormatterWithVerify<MessagePackData>().Serialize(ref writer, value.MessagePack, options);
    }

    public TypeDecoratingUniSerialized<TSchema, T> Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            throw new MessagePackSerializationException($"Unexpected nil for {typeof(TypeDecoratingUniSerialized<TSchema, T>).GetName()}.");

        options.Security.DepthStep(ref reader);
        try {
            var messagePack = default(MessagePackData);
            var count = reader.ReadArrayHeader();
            for (var i = 0; i < count; i++) {
                if (i == 0)
                    messagePack = options.Resolver.GetFormatterWithVerify<MessagePackData>().Deserialize(ref reader, options);
                else
                    reader.Skip();
            }
            return new TypeDecoratingUniSerialized<TSchema, T>(messagePack);
        }
        finally {
            reader.Depth--;
        }
    }
}
