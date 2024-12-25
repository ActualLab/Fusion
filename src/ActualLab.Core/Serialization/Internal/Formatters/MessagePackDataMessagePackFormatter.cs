using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Serialization.Internal;

public sealed class MessagePackDataMessagePackFormatter : IMessagePackFormatter<MessagePackData>
{
    public void Serialize(ref MessagePackWriter writer, MessagePackData value, MessagePackSerializerOptions options)
        => options.Resolver.GetFormatterWithVerify<byte[]>().Serialize(ref writer, value.Data, options);

    public MessagePackData Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => new(options.Resolver.GetFormatterWithVerify<byte[]>().Deserialize(ref reader, options));
}
