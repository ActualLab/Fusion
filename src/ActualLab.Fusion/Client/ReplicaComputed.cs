using ActualLab.Fusion.Interception;
using ActualLab.Internal;

namespace ActualLab.Fusion.Client;

public interface IReplicaComputed : IComputed
{
    public Computed? Original { get; }

    public void CaptureOriginal();
}

public class ReplicaComputed<T> : ComputeMethodComputed<T>, IReplicaComputed
{
    private volatile Computed? _original;

    public Computed? Original => _original;

    public ReplicaComputed(ComputedOptions options, ComputeMethodInput input)
        : base(options, input)
    { }

    protected ReplicaComputed(ComputedOptions options, ComputeMethodInput input, Result<T> output, bool isConsistent = true)
        : base(options, input, output, isConsistent)
    { }

    public void CaptureOriginal()
    {
        var dependencies = GetDependencies();
        if (dependencies.Length != 1 || dependencies[0] is not Computed<T> original)
            throw Errors.InternalError($"{nameof(IReplicaComputed)} must have a single dependency.");

        lock (Lock)
            _original = original;

        TrySetOutput(((Computed)original).Output);
    }

    protected override void OnInvalidated()
    {
        base.OnInvalidated();
        // The only purpose of CloneComputed is to do this:
        Original?.Invalidate();
    }
}
