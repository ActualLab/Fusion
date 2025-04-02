namespace ActualLab.Fusion;

public interface IStateBoundComputed : IComputed
{
    public State State { get; }
}

public class StateBoundComputed<T> : Computed<T>, IStateBoundComputed
{
    public State State { get; }

    public StateBoundComputed(ComputedOptions options, State state)
        : base(options, state)
    {
        State = state;
        ComputedRegistry.Instance.PseudoRegister(this);
    }

    protected StateBoundComputed(ComputedOptions options, State state, Result<T> output, bool isConsistent)
        : base(options, state, output, isConsistent)
    {
        State = state;
        if (isConsistent)
            ComputedRegistry.Instance.PseudoRegister(this);
    }

    protected override void OnInvalidated()
    {
        ComputedRegistry.Instance.PseudoUnregister(this);
        CancelTimeouts();
        State.OnInvalidated(this);
    }
}
