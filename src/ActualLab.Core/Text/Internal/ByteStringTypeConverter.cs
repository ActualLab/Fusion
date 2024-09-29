using System.ComponentModel;
using System.Globalization;

namespace ActualLab.Text.Internal;

// Used by JSON.NET to serialize dictionary keys of this type
public class ByteStringTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string))
            return ((ByteString) value!).ToBase64();
        return base.ConvertTo(context, culture, value, destinationType)!;
    }

    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string s)
            // ReSharper disable once HeapView.BoxingAllocation
            return ByteString.FromBase64(s);
        return base.ConvertFrom(context, culture, value)!;
    }
}
