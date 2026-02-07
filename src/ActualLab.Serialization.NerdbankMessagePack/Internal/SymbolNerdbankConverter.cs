using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="Symbol"/>.
/// </summary>
public sealed class SymbolNerdbankConverter : MessagePackConverter<Symbol>
{
    public override Symbol Read(ref MessagePackReader reader, SerializationContext context)
        => new(reader.ReadString()!);

    public override void Write(ref MessagePackWriter writer, in Symbol value, SerializationContext context)
        => writer.Write(value.Value);
}
