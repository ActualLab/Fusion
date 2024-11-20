using ActualLab.Conversion.Internal;

namespace ActualLab.Conversion;

public interface IConverter<in TSource, TTarget>
{
    public Option<TTarget> TryConvert(TSource source);
#if NETFRAMEWORK || NETSTANDARD2_0
    public TTarget Convert(TSource source);
#else
    public TTarget Convert(TSource source)
    {
        var targetOpt = TryConvert(source);
        return targetOpt.HasValue
            ? targetOpt.ValueOrDefault!
            : throw Errors.CantConvert(typeof(TSource), typeof(TTarget));
    }
#endif
}
