using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Api.Internal;

public sealed class ApiOptionMessagePackFormatter<T> : IMessagePackFormatter<ApiOption<T>>
    where T : struct
{
    public void Serialize(ref MessagePackWriter writer, ApiOption<T> value, MessagePackSerializerOptions options)
    {
        if (!value.HasValue)
            writer.WriteArrayHeader(0);
        else {
            writer.WriteArrayHeader(1);
            options.Resolver.GetFormatterWithVerify<T>().Serialize(ref writer, value.ValueOrDefault, options);
        }
    }

    public ApiOption<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var count = reader.ReadArrayHeader();
        if (count == 0)
            return default;
        if (count != 1)
            throw new MessagePackSerializationException($"Expected 0 or 1 items, but got {count}.");

        return options.Resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);
    }
}
