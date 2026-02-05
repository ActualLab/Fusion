namespace ActualLab.Fusion.Internal;

/// <summary>
/// A <see cref="ComputedState{T}"/> that computes its value using a delegate
/// of type <see cref="Func{CancellationToken, Task}"/>.
/// </summary>
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
