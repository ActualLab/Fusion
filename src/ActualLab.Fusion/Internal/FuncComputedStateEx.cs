namespace ActualLab.Fusion.Internal;

public sealed class FuncComputedStateEx<T> : ComputedState<T>
{
    public Func<ComputedState<T>, CancellationToken, Task<T>> Computer { get; }

    public FuncComputedStateEx(
        Options options,
        IServiceProvider services,
        Func<ComputedState<T>, CancellationToken, Task<T>> computer)
        : base(options, services, false)
    {
        Computer = computer;
        Initialize(options);
    }

    protected override Task Compute(CancellationToken cancellationToken)
        => GetComputeTaskIfDisposed() ?? Computer.Invoke(this, cancellationToken);
}
