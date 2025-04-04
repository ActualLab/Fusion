using ActualLab.Text.Internal;

namespace ActualLab.Text;

public sealed class StringAsSymbolMemoryPackFormatterAttribute
    : MemoryPackCustomFormatterAttribute<StringAsSymbolMemoryPackFormatter, string>
{
    public override StringAsSymbolMemoryPackFormatter GetFormatter() => StringAsSymbolMemoryPackFormatter.Default;
}
