using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Compliance.Internal;

/// <summary>
/// MessagePack formatter for <see cref="SanitizedString{TSanitizer}"/> - writes the raw value
/// exactly as a <see cref="string"/> would be written.
/// </summary>
public sealed class SanitizedStringMessagePackFormatter<TSanitizer> : IMessagePackFormatter<SanitizedString<TSanitizer>>
    where TSanitizer : Sanitizer, new()
{
    public void Serialize(
        ref MessagePackWriter writer, SanitizedString<TSanitizer> value, MessagePackSerializerOptions options)
        => options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Value, options);

    public SanitizedString<TSanitizer> Deserialize(
        ref MessagePackReader reader, MessagePackSerializerOptions options)
        => new(options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
}
