using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// A MessagePack formatter for <see cref="JsonString"/>.
/// </summary>
public sealed class JsonStringMessagePackFormatter : IMessagePackFormatter<JsonString?>
{
    public void Serialize(ref MessagePackWriter writer, JsonString? value, MessagePackSerializerOptions options)
    {
        if (value is null)
            writer.WriteNil();
        else
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Value, options);
    }

    public JsonString? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => reader.TryReadNil()
            ? null
            : new(options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
}
