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
        var builder = ImmutableDictionary.CreateBuilder<string, NewtonsoftJsonSerialized<object>>(StringComparer.Ordinal);

        // Accept both wire shapes for the inner dictionary:
        //   - map   {k: v, k: v, ...}          — what this converter (and the dynamic
        //                                          DictionaryFormatter) writes.
        //   - array [[k, v], [k, v], ...]      — what MessagePack-CSharp's source-generated
        //                                          Collection formatter emitted historically;
        //                                          kept readable so DB blobs written pre-migration
        //                                          still deserialize.
        if (reader.NextMessagePackType == MessagePackType.Array) {
            var count = reader.ReadArrayHeader();
            for (var i = 0; i < count; i++) {
                var pairLen = reader.ReadArrayHeader();
                if (pairLen != 2)
                    throw new MessagePackSerializationException(
                        $"Expected 2-element kv pair inside ImmutableOptionSet, got {pairLen}.");
                var key = reader.ReadString() ?? "";
                var value = itemConverter.Read(ref reader, context)!;
                builder[key] = value;
            }
        }
        else {
            var count = reader.ReadMapHeader();
            if (count == 0)
                return default;
            for (var i = 0; i < count; i++) {
                var key = reader.ReadString() ?? "";
                var value = itemConverter.Read(ref reader, context)!;
                builder[key] = value;
            }
        }
        return builder.Count == 0 ? default : new ImmutableOptionSet(builder.ToImmutable());
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
