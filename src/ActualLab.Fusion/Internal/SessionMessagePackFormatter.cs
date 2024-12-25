using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Fusion.Internal;

public sealed class SessionMessagePackFormatter : IMessagePackFormatter<Session?>
{
    public void Serialize(ref MessagePackWriter writer, Session? value, MessagePackSerializerOptions options)
    {
        if (ReferenceEquals(value, null))
            writer.WriteNil();
        else
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Id.Value, options);
    }

    public Session? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => reader.TryReadNil()
            ? null
            : new(options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
}
