namespace ActualLab.Conversion;

public interface IConverterProvider
{
    public ISourceConverterProvider From(Type sourceType);
    public ISourceConverterProvider<TSource> From<TSource>();
}

public abstract class ConverterProvider : IConverterProvider
{
    public static IConverterProvider Default { get; set; } =
        new ServiceCollection().AddConverters().BuildServiceProvider().GetRequiredService<IConverterProvider>();

    public ISourceConverterProvider<TSource> From<TSource>()
        => (ISourceConverterProvider<TSource>) From(typeof(TSource));
    public abstract ISourceConverterProvider From(Type sourceType);
}
