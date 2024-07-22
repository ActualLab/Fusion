namespace ActualLab.Fusion.Blazor.Internal;

public sealed class ComputedStateComponentState<T>(
    ComputedState<T>.Options settings,
    Func<CancellationToken, Task<T>> computer,
    IServiceProvider services
    ) : ComputedState<T>(settings, services, false), IHasInitialize
{
    public readonly Func<CancellationToken, Task<T>> Computer = computer;

    void IHasInitialize.Initialize(object? settings)
        => base.Initialize((Options)settings!);

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
