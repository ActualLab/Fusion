namespace ActualLab.Conversion;

/// <summary>
/// Provides access to <see cref="ISourceConverterProvider"/> instances by source type.
/// </summary>
public interface IConverterProvider
{
    public ISourceConverterProvider From(Type sourceType);
    public ISourceConverterProvider<TSource> From<TSource>();
}

/// <summary>
/// Abstract base implementation of <see cref="IConverterProvider"/>.
/// </summary>
public abstract class ConverterProvider : IConverterProvider
{
    public static IConverterProvider Default { get; set; } =
        new ServiceCollection().AddConverters().BuildServiceProvider().GetRequiredService<IConverterProvider>();

    public ISourceConverterProvider<TSource> From<TSource>()
        => (ISourceConverterProvider<TSource>) From(typeof(TSource));
    public abstract ISourceConverterProvider From(Type sourceType);
}
