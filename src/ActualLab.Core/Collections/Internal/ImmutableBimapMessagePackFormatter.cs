using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Collections.Internal;

public sealed class ImmutableBimapMessagePackFormatter<TFrom, TTo> : IMessagePackFormatter<ImmutableBimap<TFrom, TTo>?>
    where TFrom : notnull
    where TTo : notnull
{
    public void Serialize(ref MessagePackWriter writer, ImmutableBimap<TFrom, TTo>? value, MessagePackSerializerOptions options)
    {
        if (value is null) {
            writer.WriteNil();
            return;
        }
        writer.WriteArrayHeader(1);
        options.Resolver.GetFormatterWithVerify<IReadOnlyDictionary<TFrom, TTo>>()
            .Serialize(ref writer, value.Forward, options);
    }

    public ImmutableBimap<TFrom, TTo>? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        var count = reader.ReadArrayHeader();
        if (count != 1)
            throw new MessagePackSerializationException($"Expected 1 item for ImmutableBimap<,>, but got {count}.");

        var forward = options.Resolver.GetFormatterWithVerify<IReadOnlyDictionary<TFrom, TTo>>()
            .Deserialize(ref reader, options);
        return new ImmutableBimap<TFrom, TTo>(forward);
    }
}
