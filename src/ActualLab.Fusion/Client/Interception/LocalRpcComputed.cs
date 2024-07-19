using ActualLab.Fusion.Interception;
using ActualLab.Internal;

namespace ActualLab.Fusion.Client.Interception;

public interface ILocalRpcComputed
{
    Computed? Original { get; }
}

public class LocalRpcComputed<T>(ComputedOptions options, ComputeMethodInput input)
    : ComputeMethodComputed<T>(options, input), ILocalRpcComputed
{
    private volatile Computed? _original;

    public Computed? Original => _original;

    public void Dispose()
    { }

    public void CaptureOriginal()
    {
        var dependencies = GetDependencies();
        if (dependencies.Length != 1 || dependencies[0] is not Computed<T> original)
            throw Errors.InternalError("LocalRpcComputed must have a single dependency.");

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