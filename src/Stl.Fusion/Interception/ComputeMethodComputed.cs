namespace Stl.Fusion.Interception;

// Just a tagging interface
public interface IComputedMethodComputed : IComputed
{ }

public class ComputeMethodComputed<T> : Computed<T>, IComputedMethodComputed
{
    public ComputeMethodComputed(ComputedOptions options, ComputeMethodInput input, LTag version)
        : base(options, input, version)
        => ComputedRegistry.Instance.Register(this);

    protected ComputeMethodComputed(
        ComputedOptions options,
        ComputeMethodInput input,
        Result<T> output,
        LTag version,
        bool isConsistent = true)
        : base(options, input, output, version, isConsistent)
    {
        if (isConsistent)
            ComputedRegistry.Instance.Register(this);
    }

    protected override void OnInvalidated()
    {
        ComputedRegistry.Instance.Unregister(this);
        CancelTimeouts();
    }
}
