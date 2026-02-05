namespace ActualLab.Fusion.Internal;

/// <summary>
/// A <see cref="ComputedState{T}"/> that computes its value using a delegate
/// that also receives the state instance itself.
/// </summary>
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
