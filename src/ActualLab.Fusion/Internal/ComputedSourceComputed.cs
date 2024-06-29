namespace ActualLab.Fusion.Internal;

public interface IComputedSourceComputed: ComputedBase
{
    public IComputedSource Source { get; }
}

public sealed class ComputedSourceComputed<T> : Computed<T>, IComputedSourceComputed
{
    IComputedSource IComputedSourceComputed.Source => Source;
    public ComputedSource<T> Source { get; }

    public ComputedSourceComputed(ComputedOptions options, ComputedSource<T> source)
        : base(options, source)
    {
        Source = source;
        ComputedRegistry.Instance.PseudoRegister(this);
    }

    public ComputedSourceComputed(
        ComputedOptions options, ComputedSource<T> source,
        Result<T> output, bool isConsistent)
        : base(options, source, output, isConsistent)
    {
        Source = source;
        if (isConsistent)
            ComputedRegistry.Instance.PseudoRegister(this);
    }

    protected override void OnInvalidated()
    {
        ComputedRegistry.Instance.PseudoUnregister(this);
        CancelTimeouts();
        Source.OnInvalidated(this);
    }
}
