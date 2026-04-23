using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="ApiMap{TKey,TValue}"/>. Serializes as a
/// plain msgpack map <c>{k1: v1, k2: v2, ...}</c> — the same wire shape MessagePack-CSharp's
/// dynamic <c>DictionaryFormatter</c> produces for the underlying <see cref="Dictionary{TKey,TValue}"/>.
/// </summary>
public sealed class ApiMapNerdbankConverter<TKey, TValue> : MessagePackConverter<ApiMap<TKey, TValue>?>
    where TKey : notnull
{
    public override ApiMap<TKey, TValue>? Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return null;
        var map = new ApiMap<TKey, TValue>();
        var keyConverter = context.GetConverter<TKey>(context.TypeShapeProvider);
        var valueConverter = context.GetConverter<TValue>(context.TypeShapeProvider);

        // Accept both wire shapes:
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
                        $"Expected 2-element kv pair inside ApiMap<,>, got {pairLen}.");
                var k = keyConverter.Read(ref reader, context)!;
                var v = valueConverter.Read(ref reader, context)!;
                map[k] = v;
            }
        }
        else {
            var count = reader.ReadMapHeader();
            for (var i = 0; i < count; i++) {
                var k = keyConverter.Read(ref reader, context)!;
                var v = valueConverter.Read(ref reader, context)!;
                map[k] = v;
            }
        }
        return map;
    }

    public override void Write(ref MessagePackWriter writer, in ApiMap<TKey, TValue>? value, SerializationContext context)
    {
        if (value is null) {
            writer.WriteNil();
            return;
        }
        writer.WriteMapHeader(value.Count);
        if (value.Count == 0)
            return;
        var keyConverter = context.GetConverter<TKey>(context.TypeShapeProvider);
        var valueConverter = context.GetConverter<TValue>(context.TypeShapeProvider);
        // Iterate sorted order — matches ApiMap's public enumerator contract, producing stable bytes.
        foreach (var kvp in value) {
            keyConverter.Write(ref writer, kvp.Key, context);
            valueConverter.Write(ref writer, kvp.Value, context);
        }
    }
}
