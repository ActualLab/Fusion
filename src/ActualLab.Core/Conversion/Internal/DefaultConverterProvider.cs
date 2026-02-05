namespace ActualLab.Conversion.Internal;

/// <summary>
/// The default <see cref="ConverterProvider"/> that resolves
/// <see cref="ISourceConverterProvider"/> instances from the DI container.
/// </summary>
public class DefaultConverterProvider(IServiceProvider services) : ConverterProvider
{
    private readonly ConcurrentDictionary<Type, ISourceConverterProvider> _cache = new();

    protected IServiceProvider Services { get; } = services;

    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume SourceConverterProvider's methods are preserved")]
    public override ISourceConverterProvider From(Type sourceType)
        => _cache.GetOrAdd(sourceType, static (sourceType1, self) => {
            var scpType = typeof(ISourceConverterProvider<>).MakeGenericType(sourceType1);
            return (ISourceConverterProvider) self.Services.GetRequiredService(scpType);
        }, this);
}
