using ActualLab.Collections.Internal;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="PropertyBag{TSchema}"/>. Wire shape matches the legacy
/// <c>[MessagePackObject, Key(0)] PropertyBagItem<TSchema>[]? RawItems</c> formatter: a 1-element array
/// wrapping the items array. See <see cref="PropertyBagItemNerdbankConverter{TSchema}"/> for the per-item
/// shape.
/// </summary>
public sealed class PropertyBagNerdbankConverter<TSchema> : MessagePackConverter<PropertyBag<TSchema>>
    where TSchema : TypeSchema, new()
{
    public override PropertyBag<TSchema> Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return default;
        var outerLen = reader.ReadArrayHeader();
        if (outerLen != 1)
            throw new MessagePackSerializationException(
                $"Expected 1-element array for PropertyBag<TSchema>, got {outerLen}.");
        if (reader.TryReadNil())
            return default;
        var arrayLen = reader.ReadArrayHeader();
        if (arrayLen == 0)
            return default;
        var itemConverter = context.GetConverter<PropertyBagItem<TSchema>>(context.TypeShapeProvider);
        var items = new PropertyBagItem<TSchema>[arrayLen];
        for (var i = 0; i < arrayLen; i++)
            items[i] = itemConverter.Read(ref reader, context);
        return new PropertyBag<TSchema>(items);
    }

    public override void Write(ref MessagePackWriter writer, in PropertyBag<TSchema> value, SerializationContext context)
    {
        writer.WriteArrayHeader(1);
        if (value.Count == 0) {
            writer.WriteNil();
            return;
        }
        var items = value.Items;
        writer.WriteArrayHeader(items.Count);
        var itemConverter = context.GetConverter<PropertyBagItem<TSchema>>(context.TypeShapeProvider);
        foreach (var item in items)
            itemConverter.Write(ref writer, item, context);
    }
}

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="PropertyBagItem{TSchema}"/>. Legacy wire:
/// 2-element array <c>[Key (string), Serialized (TypeDecoratingUniSerialized&lt;object&gt;)]</c>.
/// The inner TypeDecoratingUniSerialized uses its own converter (registered separately) so the
/// payload transcodes through the Nerdbank-owned type-decorating serializer.
/// </summary>
public sealed class PropertyBagItemNerdbankConverter<TSchema> : MessagePackConverter<PropertyBagItem<TSchema>>
    where TSchema : TypeSchema, new()
{
    public override PropertyBagItem<TSchema> Read(ref MessagePackReader reader, SerializationContext context)
    {
        var len = reader.ReadArrayHeader();
        if (len != 2)
            throw new MessagePackSerializationException(
                $"Expected 2-element array for PropertyBagItem<TSchema>, got {len}.");
        var key = reader.ReadString() ?? "";
        var serializedConverter = context.GetConverter<TypeDecoratingUniSerialized<TSchema, object>>(context.TypeShapeProvider);
        var serialized = serializedConverter.Read(ref reader, context);
        return new PropertyBagItem<TSchema>(key, serialized);
    }

    public override void Write(ref MessagePackWriter writer, in PropertyBagItem<TSchema> value, SerializationContext context)
    {
        writer.WriteArrayHeader(2);
        writer.Write(value.Key);
        var serializedConverter = context.GetConverter<TypeDecoratingUniSerialized<TSchema, object>>(context.TypeShapeProvider);
        serializedConverter.Write(ref writer, value.Serialized, context);
    }
}
