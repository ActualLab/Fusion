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
    {
        if (IsDisposed) {
            // Once the state is disposed, any update will take indefinitely long time
            return TaskExt
                .NewNeverEndingUnreferenced<T>()
                .WaitAsync(cancellationToken);
        }
        return Computer.Invoke(cancellationToken);
    }
}
