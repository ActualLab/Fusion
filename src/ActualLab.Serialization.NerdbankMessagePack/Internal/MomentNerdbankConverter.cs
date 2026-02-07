using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="Moment"/>.
/// </summary>
public class MomentNerdbankConverter : MessagePackConverter<Moment>
{
    public override Moment Read(ref MessagePackReader reader, SerializationContext context)
        => new(reader.ReadInt64());

    public override void Write(ref MessagePackWriter writer, in Moment value, SerializationContext context)
        => writer.WriteInt64(value.EpochOffsetTicks);
}
