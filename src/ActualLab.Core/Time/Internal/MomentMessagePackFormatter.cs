using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Time.Internal;

/// <summary>
/// MessagePack formatter for <see cref="Moment"/>.
/// </summary>
public class MomentMessagePackFormatter : IMessagePackFormatter<Moment>
{
    public void Serialize(ref MessagePackWriter writer, Moment value, MessagePackSerializerOptions options)
        => writer.WriteInt64(value.EpochOffsetTicks);

    public Moment Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => new(reader.ReadInt64());
}
