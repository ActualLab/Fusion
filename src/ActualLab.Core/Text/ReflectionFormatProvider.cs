using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ActualLab.Internal;

namespace ActualLab.Text;

public sealed class ReflectionFormatProvider : IFormatProvider, ICustomFormatter
{
    private static readonly char[] Separator = { ':' };

    public static ReflectionFormatProvider Instance {
        [RequiresUnreferencedCode(UnreferencedCode.Reflection)] get;
    } = new();

    private ReflectionFormatProvider()
    { }

    public object? GetFormat(Type? formatType)
        => formatType == typeof(ICustomFormatter) ? this : null;

    public string Format(string? format, object? arg, IFormatProvider? formatProvider) {
        var formats = (format ?? string.Empty).Split(Separator, 2);
        var propertyName = formats[0].TrimEnd('}');
        var suffix = formats[0][propertyName.Length..];
        var propertyFormat = formats.Length > 1 ? formats[1] : null;

#pragma warning disable IL2075
        var getter = arg?.GetType().GetProperty(propertyName)?.GetGetMethod();
#pragma warning restore IL2075
        if (getter == null)
            return arg is IFormattable formattable
                ? formattable.ToString(format, formatProvider)
                : arg?.ToString() ?? "";

        var value = getter.Invoke(arg, null);
        return propertyFormat == null
            ? (value?.ToString() ?? "") + suffix
            : string.Format(CultureInfo.InvariantCulture, $"{{0:{propertyFormat}}}", value);
    }
}
