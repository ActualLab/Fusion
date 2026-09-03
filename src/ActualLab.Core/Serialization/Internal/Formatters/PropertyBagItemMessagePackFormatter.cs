using ActualLab.Collections.Internal;
using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// A MessagePack formatter for <see cref="PropertyBagItem{TSchema}"/>.
/// </summary>
public sealed class PropertyBagItemMessagePackFormatter<TSchema> : IMessagePackFormatter<PropertyBagItem<TSchema>>
    where TSchema : TypeSchema, new()
{
    public void Serialize(
        ref MessagePackWriter writer,
        PropertyBagItem<TSchema> value,
        MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(2);
        options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Key, options);
        options.Resolver.GetFormatterWithVerify<TypeDecoratingUniSerialized<TSchema, object>>()
            .Serialize(ref writer, value.Serialized, options);
    }

    public PropertyBagItem<TSchema> Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return default;

        options.Security.DepthStep(ref reader);
        try {
            var key = default(string);
            var serialized = default(TypeDecoratingUniSerialized<TSchema, object>);
            var count = reader.ReadArrayHeader();
            for (var i = 0; i < count; i++) {
                switch (i) {
                case 0:
                    key = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                    break;
                case 1:
                    serialized = options.Resolver.GetFormatterWithVerify<TypeDecoratingUniSerialized<TSchema, object>>()
                        .Deserialize(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
                }
            }
            return new PropertyBagItem<TSchema>(key!, serialized);
        }
        finally {
            reader.Depth--;
        }
    }
}
