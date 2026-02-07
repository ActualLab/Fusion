using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="CpuTimestamp"/>.
/// </summary>
public class CpuTimestampNerdbankConverter : MessagePackConverter<CpuTimestamp>
{
    public override CpuTimestamp Read(ref MessagePackReader reader, SerializationContext context)
        => new(reader.ReadInt64());

    public override void Write(ref MessagePackWriter writer, in CpuTimestamp value, SerializationContext context)
        => writer.WriteInt64(value.Value);
}
