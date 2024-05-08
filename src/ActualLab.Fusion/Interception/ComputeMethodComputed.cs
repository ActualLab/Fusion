using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion.Interception;

// Just a tagging interface
public interface IComputedMethodComputed : IComputed;

public class ComputeMethodComputed<T> : Computed<T>, IComputedMethodComputed
{
    public ComputeMethodComputed(ComputedOptions options, ComputeMethodInput input)
        : base(options, input)
        => ComputedRegistry.Instance.Register(this);

    protected ComputeMethodComputed(ComputedOptions options, ComputeMethodInput input, Result<T> output, bool isConsistent = true)
        : base(options, input, output, isConsistent)
    {
        if (!isConsistent)
            return;

        ComputedRegistry.Instance.Register(this);
    }

    protected ComputeMethodComputed(
        ComputedOptions options,
        ComputeMethodInput input,
        Result<T> output,
        bool isConsistent,
        SkipComputedRegistration _
        ) : base(options, input, output, isConsistent)
    { }

    protected override void OnInvalidated()
    {
        ComputedRegistry.Instance.Unregister(this);
        CancelTimeouts();
    }
}
