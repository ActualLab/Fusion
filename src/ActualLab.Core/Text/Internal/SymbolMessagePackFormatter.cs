using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Text.Internal;

/// <summary>
/// MessagePack formatter for <see cref="Symbol"/>.
/// </summary>
public sealed class SymbolMessagePackFormatter : IMessagePackFormatter<Symbol>
{
    public void Serialize(ref MessagePackWriter writer, Symbol value, MessagePackSerializerOptions options)
        => options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Value, options);

    public Symbol Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => new(options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
}
