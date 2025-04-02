namespace ActualLab.Fusion.Internal;

public sealed class FuncComputedState<T> : ComputedState<T>
{
    public Func<CancellationToken, Task<T>> Computer { get; }

    public FuncComputedState(
        Options options,
        IServiceProvider services,
        Func<CancellationToken, Task<T>> computer)
        : base(options, services, false)
    {
        Computer = computer;
        Initialize(options);
    }

    protected override Task Compute(CancellationToken cancellationToken)
        => GetComputeTaskIfDisposed() ?? Computer.Invoke(cancellationToken);
}
