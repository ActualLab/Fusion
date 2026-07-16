using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion.Interception;

// Just a tagging interface

/// <summary>
/// A tagging interface for <see cref="Computed"/> instances produced by compute methods.
/// </summary>
public interface IComputedMethodComputed : IComputed;

/// <summary>
/// A <see cref="Computed{T}"/> produced by a compute method interception,
/// which auto-registers and unregisters itself in <see cref="ComputedRegistry"/>.
/// </summary>
public class ComputeMethodComputed<T> : Computed<T>, IComputedMethodComputed
{
    private protected WeakReference<Computed> RegistrySlot { get; }

    public ComputeMethodComputed(ComputedOptions options, ComputeMethodInput input)
        : base(options, input)
    {
        RegistrySlot = new(this);
        ComputedRegistry.Register(this, RegistrySlot);
    }

    protected ComputeMethodComputed(ComputedOptions options, ComputeMethodInput input, Result output, bool isConsistent = true)
        : base(options, input, output, isConsistent)
    {
        RegistrySlot = new(this);
        if (!isConsistent)
            return;

        ComputedRegistry.Register(this, RegistrySlot);
    }

    protected ComputeMethodComputed(
        ComputedOptions options,
        ComputeMethodInput input,
        Result output,
        bool isConsistent,
        SkipComputedRegistration _
        ) : base(options, input, output, isConsistent)
        => RegistrySlot = new(this);

    protected override void OnInvalidated()
    {
        ComputedRegistry.Unregister(this, RegistrySlot);
        CancelTimeouts();
    }
}
