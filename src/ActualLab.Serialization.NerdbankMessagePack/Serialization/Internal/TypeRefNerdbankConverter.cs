using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="TypeRef"/>.
/// </summary>
public sealed class TypeRefNerdbankConverter : MessagePackConverter<TypeRef>
{
    public override TypeRef Read(ref MessagePackReader reader, SerializationContext context)
        => new(reader.ReadString()!);

    public override void Write(ref MessagePackWriter writer, in TypeRef value, SerializationContext context)
        => writer.Write(value.AssemblyQualifiedName);
}
