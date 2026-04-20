using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Serialization.Internal;

public sealed class BoxMessagePackFormatter<T> : IMessagePackFormatter<Box<T>?>
{
    public void Serialize(ref MessagePackWriter writer, Box<T>? value, MessagePackSerializerOptions options)
    {
        if (value is null) {
            writer.WriteNil();
            return;
        }
        writer.WriteArrayHeader(1);
        options.Resolver.GetFormatterWithVerify<T>().Serialize(ref writer, value.Value, options);
    }

    public Box<T>? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        var count = reader.ReadArrayHeader();
        if (count != 1)
            throw new MessagePackSerializationException($"Expected 1 item for Box<>, but got {count}.");

        var value = options.Resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);
        return new Box<T>(value);
    }
}
