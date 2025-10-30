using ActualLab.Fusion.Interception;
using ActualLab.Internal;

namespace ActualLab.Fusion.Client;

/// <summary>
/// A computed that clones the <see cref="Original"/> and invalidates it on its own invalidation.
/// If the <see cref="Original"/> isn't captured, it behaves like a regular <see cref="Computed"/>.
/// </summary>
public interface IReplicaComputed : IComputed
{
    public Computed? Original { get; }

    public void CaptureOriginal();
}

/// <summary>
/// A computed that clones the <see cref="Original"/> and invalidates it on its own invalidation.
/// If the <see cref="Original"/> isn't captured, it behaves like a regular <see cref="Computed"/>.
/// </summary>
/// <typeparam name="T">The type of <see cref="Result"/>.</typeparam>
public sealed class ReplicaComputed<T> : ComputeMethodComputed<T>, IReplicaComputed
{
    private volatile Computed? _original;

    public Computed? Original => _original;

    // ReSharper disable once ConvertToPrimaryConstructor
    public ReplicaComputed(ComputedOptions options, ComputeMethodInput input)
        : base(options, input)
    { }

    public void CaptureOriginal()
    {
        var dependencies = GetDependencies();
        if (dependencies is not [Computed<T> original])
            throw Errors.InternalError(
                $"{nameof(IReplicaComputed)} dependency count != 1: [{dependencies.ToDelimitedString()}].");

        lock (Lock)
            _original = original;

        TrySetOutput(((Computed)original).Output);
    }

    protected override void OnInvalidated()
    {
        base.OnInvalidated();
        // That's the main purpose of ReplicaComputed:
        // it is used in RemoteComputedMethodFunction.ProduceComputedImpl, and there are two possible scenarios:
        // - Either it is used as the actual computed (Distributed service case),
        // - Or it is used as a replica of the original computed (DistributedPair service case).
        // In the second case, when IService.Method(...) is called rather than Service.Method(...) inside the
        // Invalidation.Begin() block, the replica of the original computed will be fetched from cache instead of
        // the original computed, and we need to invalidate it to handle this scenario.
        Original?.Invalidate();
    }
}
