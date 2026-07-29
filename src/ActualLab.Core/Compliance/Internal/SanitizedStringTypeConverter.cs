using System.ComponentModel;
using System.Globalization;

namespace ActualLab.Compliance.Internal;

// Used by JSON.NET to serialize dictionary keys of this type

/// <summary>
/// TypeConverter for <see cref="SanitizedString{TSanitizer}"/>, enabling string-based conversion.
/// </summary>
/// <remarks>
/// Converting to string yields the raw value, not the masked one: this feeds serialization, and a
/// masked value on the wire would be data loss. <see cref="object.ToString"/> is the masked path.
/// </remarks>
public class SanitizedStringTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object ConvertTo(
        ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string))
            return ((ISanitizedString)value!).Value;
        return base.ConvertTo(context, culture, value, destinationType)!;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume converter code is preserved")]
    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string s && context?.PropertyDescriptor?.PropertyType is { } type)
            return type.CreateInstance(s);
        return base.ConvertFrom(context, culture, value)!;
    }
}
