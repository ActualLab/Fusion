namespace ActualLab.Fusion.Internal;

public sealed class FuncComputedStateEx<T> : ComputedState<T>
{
    public Func<ComputedState<T>, CancellationToken, Task<T>> Computer { get; }

    public FuncComputedStateEx(
        Options settings,
        IServiceProvider services,
        Func<ComputedState<T>, CancellationToken, Task<T>> computer)
        : base(settings, services, false)
    {
        Computer = computer;
        Initialize(settings);
    }

    protected override Task Compute(CancellationToken cancellationToken)
        => GetComputeTaskIfDisposed() ?? Computer.Invoke(this, cancellationToken);
}
