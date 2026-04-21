using ActualLab.Collections;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="ImmutableOptionSet"/>. Wire shape matches the
/// legacy MessagePack-CSharp formatter: a 1-element array wrapping the
/// <c>JsonCompatibleItems</c> dictionary, each value a <see cref="NewtonsoftJsonSerialized{T}"/>
/// (itself serialized as a 1-element array containing the JSON string — see Nerdbank's default
/// reflection shape for TextSerialized{T}).
/// </summary>
public sealed class ImmutableOptionSetNerdbankConverter : MessagePackConverter<ImmutableOptionSet>
{
    public override ImmutableOptionSet Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return default;
        var outerLen = reader.ReadArrayHeader();
        if (outerLen != 1)
            throw new MessagePackSerializationException(
                $"Expected 1-element array for ImmutableOptionSet, got {outerLen}.");
        if (reader.TryReadNil())
            return default;
        var itemConverter = context.GetConverter<NewtonsoftJsonSerialized<object>>(context.TypeShapeProvider);
        var count = reader.ReadMapHeader();
        if (count == 0)
            return default;
        var builder = ImmutableDictionary.CreateBuilder<string, NewtonsoftJsonSerialized<object>>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++) {
            var key = reader.ReadString() ?? "";
            var value = itemConverter.Read(ref reader, context)!;
            builder[key] = value;
        }
        return new ImmutableOptionSet(builder.ToImmutable());
    }

    public override void Write(ref MessagePackWriter writer, in ImmutableOptionSet value, SerializationContext context)
    {
        writer.WriteArrayHeader(1);
        var json = value.JsonCompatibleItems;
        writer.WriteMapHeader(json.Count);
        if (json.Count == 0)
            return;
        var itemConverter = context.GetConverter<NewtonsoftJsonSerialized<object>>(context.TypeShapeProvider);
        foreach (var (k, v) in json) {
            writer.Write(k);
            itemConverter.Write(ref writer, v, context);
        }
    }
}
