namespace ActualLab.Conversion;

/// <summary>
/// Provides converters from a specific source type to various target types.
/// </summary>
public interface ISourceConverterProvider
{
    public Type SourceType { get; }
    public Converter To(Type targetType);
    public Converter To<TTarget>();
}

/// <summary>
/// A typed <see cref="ISourceConverterProvider"/> that provides converters
/// from <typeparamref name="TSource"/> to various target types.
/// </summary>
public interface ISourceConverterProvider<TSource> : ISourceConverterProvider
{
    public new Converter<TSource> To(Type targetType);
    public new Converter<TSource, TTarget> To<TTarget>();
}

/// <summary>
/// Abstract base implementation of <see cref="ISourceConverterProvider{TSource}"/>.
/// </summary>
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
