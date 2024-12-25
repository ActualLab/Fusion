using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Identifiers.Internal;

public sealed class SymbolIdentifierMessagePackFormatter<TIdentifier> : IMessagePackFormatter<TIdentifier>
    where TIdentifier : struct, ISymbolIdentifier<TIdentifier>
{
    public void Serialize(ref MessagePackWriter writer, TIdentifier value, MessagePackSerializerOptions options)
        => options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Value, options);

    public TIdentifier Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => TIdentifier.Parse(options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
}
