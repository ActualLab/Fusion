using ActualLab.Collections.Internal;
using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// A MessagePack formatter for <see cref="PropertyBag{TSchema}"/>.
/// </summary>
public sealed class PropertyBagMessagePackFormatter<TSchema> : IMessagePackFormatter<PropertyBag<TSchema>>
    where TSchema : TypeSchema, new()
{
    public void Serialize(
        ref MessagePackWriter writer,
        PropertyBag<TSchema> value,
        MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(1);
        options.Resolver.GetFormatterWithVerify<PropertyBagItem<TSchema>[]?>()
            .Serialize(ref writer, value.RawItems, options);
    }

    public PropertyBag<TSchema> Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            throw new MessagePackSerializationException($"Unexpected nil for {typeof(PropertyBag<TSchema>).GetName()}.");

        options.Security.DepthStep(ref reader);
        try {
            var rawItems = default(PropertyBagItem<TSchema>[]);
            var count = reader.ReadArrayHeader();
            for (var i = 0; i < count; i++) {
                if (i == 0)
                    rawItems = options.Resolver.GetFormatterWithVerify<PropertyBagItem<TSchema>[]?>()
                        .Deserialize(ref reader, options);
                else
                    reader.Skip();
            }
            return new PropertyBag<TSchema>(rawItems);
        }
        finally {
            reader.Depth--;
        }
    }
}
