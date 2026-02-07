using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="JsonString"/>.
/// </summary>
public sealed class JsonStringNerdbankConverter : MessagePackConverter<JsonString?>
{
    public override JsonString? Read(ref MessagePackReader reader, SerializationContext context)
        => reader.TryReadNil()
            ? null
            : new(reader.ReadString()!);

    public override void Write(ref MessagePackWriter writer, in JsonString? value, SerializationContext context)
    {
        if (value is null)
            writer.WriteNil();
        else
            writer.Write(value.Value);
    }
}
