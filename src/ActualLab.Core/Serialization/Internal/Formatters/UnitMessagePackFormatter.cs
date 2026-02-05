using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// A MessagePack formatter for <see cref="Unit"/>.
/// </summary>
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
