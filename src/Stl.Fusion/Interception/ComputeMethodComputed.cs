using ActualLab.Fusion.Internal;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Fusion.Interception;

// Just a tagging interface
public interface IComputedMethodComputed : IComputed;

public class ComputeMethodComputed<T> : Computed<T>, IComputedMethodComputed
{
    public ComputeMethodComputed(ComputedOptions options, ComputeMethodInput input, LTag version)
        : base(options, input, version)
    {
        input.ThrowIfDisposed();
        ComputedRegistry.Instance.Register(this);
    }

    protected ComputeMethodComputed(
        ComputedOptions options,
        ComputeMethodInput input,
        Result<T> output,
        LTag version,
        bool isConsistent = true)
        : base(options, input, output, version, isConsistent)
    {
        if (!isConsistent)
            return;

        input.ThrowIfDisposed();
        ComputedRegistry.Instance.Register(this);
    }

    protected ComputeMethodComputed(
        ComputedOptions options,
        ComputeMethodInput input,
        Result<T> output,
        LTag version,
        bool isConsistent,
        SkipComputedRegistration _)
        : base(options, input, output, version, isConsistent)
    { }

    protected override void OnInvalidated()
    {
        ComputedRegistry.Instance.Unregister(this);
        CancelTimeouts();
    }
}
