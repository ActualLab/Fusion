using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.IO.Internal;

public sealed class FilePathMessagePackFormatter : IMessagePackFormatter<FilePath>
{
    public void Serialize(ref MessagePackWriter writer, FilePath value, MessagePackSerializerOptions options)
        => options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Value, options);

    public FilePath Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => new(options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
}
