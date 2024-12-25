using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Serialization.Internal;

public class UnitMessagePackFormatter : IMessagePackFormatter<Unit>
{
    public void Serialize(ref MessagePackWriter writer, Unit value, MessagePackSerializerOptions options)
        => writer.WriteNil();

    public Unit Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        reader.ReadNil();
        return default;
    }
}
