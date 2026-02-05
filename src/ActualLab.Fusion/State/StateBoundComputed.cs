namespace ActualLab.Fusion;

/// <summary>
/// An <see cref="IComputed"/> that is bound to a <see cref="State"/>.
/// </summary>
public interface IStateBoundComputed : IComputed
{
    public State State { get; }
}

/// <summary>
/// A <see cref="Computed{T}"/> produced by and bound to a <see cref="State"/>,
/// notifying the state on invalidation.
/// </summary>
public class StateBoundComputed<T> : Computed<T>, IStateBoundComputed
{
    public State State { get; }

    public StateBoundComputed(ComputedOptions options, State state)
        : base(options, state)
    {
        State = state;
        ComputedRegistry.PseudoRegister(this);
    }

    protected StateBoundComputed(ComputedOptions options, State state, Result output, bool isConsistent)
        : base(options, state, output, isConsistent)
    {
        State = state;
        if (isConsistent)
            ComputedRegistry.PseudoRegister(this);
    }

    protected override void OnInvalidated()
    {
        ComputedRegistry.PseudoUnregister(this);
        CancelTimeouts();
        State.OnInvalidated(this);
    }
}
