namespace ActualLab.Fusion;

public interface IStateBoundComputed : IComputed
{
    public IState State { get; }
}

public class StateBoundComputed<T> : Computed<T>, IStateBoundComputed
{
    IState IStateBoundComputed.State => State;
    public State<T> State { get; }

    public StateBoundComputed(ComputedOptions options, State<T> state)
        : base(options, state)
    {
        State = state;
        ComputedRegistry.Instance.PseudoRegister(this);
    }

    protected StateBoundComputed(ComputedOptions options, State<T> state, Result<T> output, bool isConsistent)
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
