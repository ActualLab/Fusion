using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Reflection.Internal;

public sealed class TypeRefMessagePackFormatter : IMessagePackFormatter<TypeRef>
{
    public void Serialize(ref MessagePackWriter writer, TypeRef value, MessagePackSerializerOptions options)
        => options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.AssemblyQualifiedName.Value, options);

    public TypeRef Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => new(options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
}
