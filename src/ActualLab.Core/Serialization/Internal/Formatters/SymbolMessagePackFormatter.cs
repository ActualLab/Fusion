using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// A MessagePack formatter for <see cref="HostId"/>.
/// </summary>
public sealed class HostIdMessagePackFormatter : IMessagePackFormatter<HostId?>
{
    public void Serialize(ref MessagePackWriter writer, HostId? value, MessagePackSerializerOptions options)
    {
        if (ReferenceEquals(value, null))
            writer.WriteNil();
        else
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Id, options);
    }

    public HostId? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => reader.TryReadNil()
            ? null
            : new(options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
}
