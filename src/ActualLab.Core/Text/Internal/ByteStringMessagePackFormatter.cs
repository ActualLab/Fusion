using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Text.Internal;

public sealed class ByteStringMessagePackFormatter : IMessagePackFormatter<ByteString>
{
    public void Serialize(ref MessagePackWriter writer, ByteString value, MessagePackSerializerOptions options)
        => options.Resolver.GetFormatterWithVerify<ReadOnlyMemory<byte>>().Serialize(ref writer, value.Bytes, options);

    public ByteString Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => new(options.Resolver.GetFormatterWithVerify<ReadOnlyMemory<byte>>().Deserialize(ref reader, options));
}
