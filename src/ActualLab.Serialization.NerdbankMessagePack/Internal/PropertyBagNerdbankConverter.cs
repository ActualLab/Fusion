using ActualLab.Collections;
using ActualLab.Collections.Internal;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="PropertyBag"/>. Wire shape matches the legacy
/// <c>[MessagePackObject, Key(0)] PropertyBagItem[]? RawItems</c> formatter: a 1-element array
/// wrapping the items array. See <see cref="PropertyBagItemNerdbankConverter"/> for the per-item
/// shape.
/// </summary>
public sealed class PropertyBagNerdbankConverter : MessagePackConverter<PropertyBag>
{
    public override PropertyBag Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return default;
        var outerLen = reader.ReadArrayHeader();
        if (outerLen != 1)
            throw new MessagePackSerializationException(
                $"Expected 1-element array for PropertyBag, got {outerLen}.");
        if (reader.TryReadNil())
            return default;
        var arrayLen = reader.ReadArrayHeader();
        if (arrayLen == 0)
            return default;
        var itemConverter = context.GetConverter<PropertyBagItem>(context.TypeShapeProvider);
        var items = new PropertyBagItem[arrayLen];
        for (var i = 0; i < arrayLen; i++)
            items[i] = itemConverter.Read(ref reader, context);
        return new PropertyBag(items);
    }

    public override void Write(ref MessagePackWriter writer, in PropertyBag value, SerializationContext context)
    {
        writer.WriteArrayHeader(1);
        if (value.Count == 0) {
            writer.WriteNil();
            return;
        }
        var items = value.Items;
        writer.WriteArrayHeader(items.Count);
        var itemConverter = context.GetConverter<PropertyBagItem>(context.TypeShapeProvider);
        foreach (var item in items)
            itemConverter.Write(ref writer, item, context);
    }
}

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="PropertyBagItem"/>. Legacy wire:
/// 2-element array <c>[Key (string), Serialized (TypeDecoratingUniSerialized&lt;object&gt;)]</c>.
/// The inner TypeDecoratingUniSerialized uses its own converter (registered separately) so the
/// payload transcodes through the Nerdbank-owned type-decorating serializer.
/// </summary>
public sealed class PropertyBagItemNerdbankConverter : MessagePackConverter<PropertyBagItem>
{
    public override PropertyBagItem Read(ref MessagePackReader reader, SerializationContext context)
    {
        var len = reader.ReadArrayHeader();
        if (len != 2)
            throw new MessagePackSerializationException(
                $"Expected 2-element array for PropertyBagItem, got {len}.");
        var key = reader.ReadString() ?? "";
        var serializedConverter = context.GetConverter<TypeDecoratingUniSerialized<object>>(context.TypeShapeProvider);
        var serialized = serializedConverter.Read(ref reader, context);
        return new PropertyBagItem(key, serialized);
    }

    public override void Write(ref MessagePackWriter writer, in PropertyBagItem value, SerializationContext context)
    {
        writer.WriteArrayHeader(2);
        writer.Write(value.Key);
        var serializedConverter = context.GetConverter<TypeDecoratingUniSerialized<object>>(context.TypeShapeProvider);
        serializedConverter.Write(ref writer, value.Serialized, context);
    }
}
