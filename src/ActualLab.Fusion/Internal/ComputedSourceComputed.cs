namespace ActualLab.Fusion.Internal;

/// <summary>
/// An <see cref="IComputed"/> that is backed by an <see cref="IComputedSource"/>.
/// </summary>
public interface IComputedSourceComputed: IComputed
{
    public IComputedSource Source { get; }
}

/// <summary>
/// A <see cref="Computed{T}"/> produced by a <see cref="ComputedSource{T}"/>.
/// </summary>
public sealed class ComputedSourceComputed<T> : Computed<T>, IComputedSourceComputed
{
    IComputedSource IComputedSourceComputed.Source => Source;
    public ComputedSource<T> Source { get; }

    public ComputedSourceComputed(ComputedOptions options, ComputedSource<T> source)
        : base(options, source)
    {
        Source = source;
        ComputedRegistry.PseudoRegister(this);
    }

    public ComputedSourceComputed(
        ComputedOptions options, ComputedSource<T> source,
        Result output, bool isConsistent)
        : base(options, source, output, isConsistent)
    {
        Source = source;
        if (isConsistent)
            ComputedRegistry.PseudoRegister(this);
    }

    protected override void OnInvalidated()
    {
        ComputedRegistry.PseudoUnregister(this);
        CancelTimeouts();
        Source.OnInvalidated(this);
    }
}
