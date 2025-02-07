namespace ActualLab.Fusion.Internal;

public sealed class FuncComputedState<T> : ComputedState<T>
{
    public Func<CancellationToken, Task<T>> Computer { get; }

    public FuncComputedState(
        Options settings,
        IServiceProvider services,
        Func<CancellationToken, Task<T>> computer)
        : base(settings, services, false)
    {
        Computer = computer;
        Initialize(settings);
    }

    protected override Task<T> Compute(CancellationToken cancellationToken)
        => ComputeTaskIfDisposed() ?? Computer.Invoke(cancellationToken);
}
