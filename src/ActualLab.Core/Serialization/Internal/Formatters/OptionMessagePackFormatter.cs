using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Serialization.Internal;

public sealed class OptionMessagePackFormatter<T> : IMessagePackFormatter<Option<T>>
    where T : struct
{
    public void Serialize(ref MessagePackWriter writer, Option<T> value, MessagePackSerializerOptions options)
    {
        if (!value.HasValue)
            writer.WriteArrayHeader(0);
        else {
            writer.WriteArrayHeader(1);
            options.Resolver.GetFormatterWithVerify<T>().Serialize(ref writer, value.ValueOrDefault, options);
        }
    }

    public Option<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var count = reader.ReadArrayHeader();
        if (count == 0)
            return default;
        if (count != 1)
            throw new MessagePackSerializationException($"Expected 0 or 1 items, but got {count}.");

        return options.Resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);
    }
}
