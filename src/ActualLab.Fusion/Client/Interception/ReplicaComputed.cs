using ActualLab.Fusion.Interception;
using ActualLab.Internal;

namespace ActualLab.Fusion.Client.Interception;

public interface IReplicaComputed
{
    Computed? Original { get; }
}

public class ReplicaComputed<T>(ComputedOptions options, ComputeMethodInput input)
    : ComputeMethodComputed<T>(options, input), IReplicaComputed
{
    private volatile Computed? _original;

    public Computed? Original => _original;

    public void Dispose()
    { }

    public void CaptureOriginal()
    {
        var dependencies = GetDependencies();
        if (dependencies.Length != 1 || dependencies[0] is not Computed<T> original)
            throw Errors.InternalError($"{nameof(IReplicaComputed)} must have a single dependency.");

        lock (Lock)
            _original = original;

        TrySetOutput(original.Output);
    }

    protected override void OnInvalidated()
    {
        base.OnInvalidated();
        // The only purpose of CloneComputed is to do this:
        Original?.Invalidate();
    }
}
