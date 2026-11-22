using ActualLab.Text.Internal;

namespace ActualLab.Text;

/// <summary>
/// Attribute that applies <see cref="StringAsSymbolMemoryPackFormatter"/> to a string field
/// or property for MemoryPack serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class StringAsSymbolMemoryPackFormatterAttribute
    : MemoryPackCustomFormatterAttribute<IMemoryPackFormatter<string>, string>
{
#if NET9_0_OR_GREATER
    private static readonly Lock Lock = new();
#else
    private static readonly object Lock = new();
#endif

    // Each ActualLab formatter captures it once on its first use, as MemoryPack-generated code does,
    // so set it before the first serialization
    public static MemoryPackFormatter<string> Formatter {
        get;
        set {
            lock (Lock)
                field = value;
        }
    } = MemoryPack.Formatters.StringFormatter.Default;

    public static bool IsEnabled {
        get => Formatter is StringAsSymbolMemoryPackFormatter;
        set => Formatter = value
            ? StringAsSymbolMemoryPackFormatter.Default
            : MemoryPack.Formatters.StringFormatter.Default;
    }

    public override IMemoryPackFormatter<string> GetFormatter()
        => Formatter;
}
