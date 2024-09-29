namespace ActualLab.Conversion.Internal;

public class FuncConverter<TSource, TTarget>(
    Func<TSource, TTarget> converter,
    Func<TSource, Option<TTarget>> tryConverter
    ) : Converter<TSource, TTarget>
{
    public Func<TSource, TTarget> Converter { get; init; } = converter;
    public Func<TSource, Option<TTarget>> TryConverter { get; init; } = tryConverter;

    public override TTarget Convert(TSource source)
        => Converter(source);
    public override object? ConvertUntyped(object? source)
        => Converter((TSource) source!);

    public override Option<TTarget> TryConvert(TSource source)
        => TryConverter(source).Cast<TTarget>();
    public override Option<object?> TryConvertUntyped(object? source)
        => source is TSource t ? TryConverter(t).Cast<object?>() : Option<object?>.None;
}

public static class FuncConverter<TSource>
{
    public static FuncConverter<TSource, TTarget> New<TTarget>(Func<TSource, TTarget> converter)
        => new(converter, ToTryConvert(converter));
    public static FuncConverter<TSource, TTarget> New<TTarget>(
        Func<TSource, Option<TTarget>> tryConverter,
        Func<TSource, TTarget>? converter)
        => new(converter ?? FromTryConvert(tryConverter), tryConverter);

    public static Func<TSource, TTarget> FromTryConvert<TTarget>(Func<TSource, Option<TTarget>> converter)
        => s => {
            var targetOpt = converter(s);
            return targetOpt.HasValue
                ? targetOpt.ValueOrDefault!
                : throw Errors.CantConvert(typeof(TSource), typeof(TTarget));
        };

    public static Func<TSource, Option<TTarget>> ToTryConvert<TTarget>(Func<TSource, TTarget> converter)
        => s => {
            try {
                return converter(s);
            }
            catch {
                // Intended
                return Option<TTarget>.None;
            }
        };
}
