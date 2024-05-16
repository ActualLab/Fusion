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

    protected override Task<T> Compute(CancellationToken cancellationToken)
    {
        if (IsDisposed) {
            // Once the state is disposed, any update will take indefinitely long time
            return TaskExt
                .NewNeverEndingUnreferenced<T>()
                .WaitAsync(cancellationToken);
        }
        return Computer.Invoke(this, cancellationToken);
    }
}
