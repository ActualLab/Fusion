using ActualLab.Conversion.Internal;

namespace ActualLab.Conversion;

/// <summary>
/// Extension methods for <see cref="Converter"/> types.
/// </summary>
public static class ConverterExt
{
    public static Converter ThrowIfUnavailable(this Converter converter)
        => converter.IsAvailable
            ? converter
            : throw Errors.NoConverter(converter.SourceType, converter.TargetType);

    public static Converter<TSource> ThrowIfUnavailable<TSource>(this Converter<TSource> converter)
        => converter.IsAvailable
            ? converter
            : throw Errors.NoConverter(converter.SourceType, converter.TargetType);

    public static Converter<TSource, TTarget> ThrowIfUnavailable<TSource, TTarget>(this Converter<TSource, TTarget> converter)
        => converter.IsAvailable
            ? converter
            : throw Errors.NoConverter(converter.SourceType, converter.TargetType);
}
