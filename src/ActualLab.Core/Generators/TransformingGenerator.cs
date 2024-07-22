namespace ActualLab.Generators;

public sealed class TransformingGenerator<TIn, TOut>(Generator<TIn> source, Func<TIn, TOut> transformer)
    : Generator<TOut>
{
    private readonly Generator<TIn> _source = source ?? throw new ArgumentNullException(nameof(source));
    private readonly Func<TIn, TOut> _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));

    public override TOut Next()
        => _transformer(_source.Next());
}
