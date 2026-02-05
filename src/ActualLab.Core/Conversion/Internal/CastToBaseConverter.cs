namespace ActualLab.Conversion.Internal;

/// <summary>
/// A converter that performs an implicit upcast from a derived type to its base type.
/// </summary>
public class CastToBaseConverter<TSource, TTarget> : Converter<TSource, TTarget>
    where TSource : TTarget
{
    public static readonly CastToBaseConverter<TSource, TTarget> Instance = new();

    public override TTarget Convert(TSource source)
        => source;
    public override object? ConvertUntyped(object? source)
        => source;

    public override Option<TTarget> TryConvert(TSource source)
        => source;
    public override Option<object?> TryConvertUntyped(object? source)
        => (object?) (TTarget?) source;
}
