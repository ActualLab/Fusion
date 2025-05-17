using ActualLab.Text.Internal;

namespace ActualLab.Text;

#if !NETSTANDARD2_0

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class StringAsSymbolMemoryPackFormatterAttribute
    : MemoryPackCustomFormatterAttribute<IMemoryPackFormatter<string>, string>
{
#if NET9_0_OR_GREATER
    private static readonly Lock Lock = new();
#else
    private static readonly object Lock = new();
#endif

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

#else

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class StringAsSymbolMemoryPackFormatterAttribute : Attribute
{
    public static bool IsEnabled { get; set; } = false;
}

#endif
