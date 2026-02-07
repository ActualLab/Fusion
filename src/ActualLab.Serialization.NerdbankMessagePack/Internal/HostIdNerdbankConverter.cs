using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="HostId"/>.
/// </summary>
public sealed class HostIdNerdbankConverter : MessagePackConverter<HostId?>
{
    public override HostId? Read(ref MessagePackReader reader, SerializationContext context)
        => reader.TryReadNil()
            ? null
            : new(reader.ReadString()!);

    public override void Write(ref MessagePackWriter writer, in HostId? value, SerializationContext context)
    {
        if (ReferenceEquals(value, null))
            writer.WriteNil();
        else
            writer.Write(value.Id);
    }
}
