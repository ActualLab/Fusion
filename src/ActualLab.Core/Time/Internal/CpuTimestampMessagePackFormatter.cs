using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Time.Internal;

/// <summary>
/// MessagePack formatter for <see cref="CpuTimestamp"/>.
/// </summary>
public class CpuTimestampMessagePackFormatter : IMessagePackFormatter<CpuTimestamp>
{
    public void Serialize(ref MessagePackWriter writer, CpuTimestamp value, MessagePackSerializerOptions options)
        => writer.WriteInt64(value.Value);

    public CpuTimestamp Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => reader.TryReadNil() ? default : new(reader.ReadInt64());
}
