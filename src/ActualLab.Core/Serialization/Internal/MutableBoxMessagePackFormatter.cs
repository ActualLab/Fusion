using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Serialization.Internal;

public sealed class MutableBoxMessagePackFormatter<T> : IMessagePackFormatter<MutableBox<T>?>
{
    public void Serialize(ref MessagePackWriter writer, MutableBox<T>? value, MessagePackSerializerOptions options)
    {
        if (value is null) {
            writer.WriteNil();
            return;
        }
        writer.WriteArrayHeader(1);
        options.Resolver.GetFormatterWithVerify<T>().Serialize(ref writer, value.Value, options);
    }

    public MutableBox<T>? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        var count = reader.ReadArrayHeader();
        if (count != 1)
            throw new MessagePackSerializationException($"Expected 1 item for MutableBox<>, but got {count}.");

        var value = options.Resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);
        return new MutableBox<T>(value);
    }
}
