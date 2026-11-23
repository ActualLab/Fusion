using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="Unit"/>.
/// </summary>
public class UnitNerdbankConverter : MessagePackConverter<Unit>
{
    public override Unit Read(ref MessagePackReader reader, SerializationContext context)
    {
        reader.ReadNil();
        return default;
    }

    public override void Write(ref MessagePackWriter writer, in Unit value, SerializationContext context)
        => writer.WriteNil();
}
