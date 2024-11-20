namespace ActualLab.Conversion;

public interface ISourceConverterProvider
{
    public Type SourceType { get; }
    public Converter To(Type targetType);
    public Converter To<TTarget>();
}

public interface ISourceConverterProvider<TSource> : ISourceConverterProvider
{
    public new Converter<TSource> To(Type targetType);
    public new Converter<TSource, TTarget> To<TTarget>();
}

public abstract class SourceConverterProvider<TSource> : ISourceConverterProvider<TSource>
{
    public Type SourceType { get; }

    Converter ISourceConverterProvider.To(Type targetType) => To(targetType);
    Converter ISourceConverterProvider.To<TTarget>() => To(typeof(TTarget));

    public Converter<TSource, TTarget> To<TTarget>()
        => (Converter<TSource, TTarget>) To(typeof(TTarget));

    public abstract Converter<TSource> To(Type targetType);

    // ReSharper disable once ConvertConstructorToMemberInitializers
    protected SourceConverterProvider()
        => SourceType = typeof(TSource);
}
