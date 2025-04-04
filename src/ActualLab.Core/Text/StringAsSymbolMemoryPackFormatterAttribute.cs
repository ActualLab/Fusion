using ActualLab.Text.Internal;

namespace ActualLab.Text;

#if !NETSTANDARD2_0

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class StringAsSymbolMemoryPackFormatterAttribute
    : MemoryPackCustomFormatterAttribute<StringAsSymbolMemoryPackFormatter, string>
{
    public override StringAsSymbolMemoryPackFormatter GetFormatter() => StringAsSymbolMemoryPackFormatter.Default;
}

#else

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class StringAsSymbolMemoryPackFormatterAttribute : Attribute;

#endif
