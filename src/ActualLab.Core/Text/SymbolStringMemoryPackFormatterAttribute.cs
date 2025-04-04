using ActualLab.Text.Internal;

namespace ActualLab.Text;

public sealed class SymbolStringMemoryPackFormatterAttribute
    : MemoryPackCustomFormatterAttribute<SymbolStringMemoryPackFormatter, string>
{
    public override SymbolStringMemoryPackFormatter GetFormatter() => SymbolStringMemoryPackFormatter.Default;
}
