namespace ActualLab.Fusion.Internal;

public interface IComputedApplyHandler<in TArg, out TResult>
{
    public TResult Apply<T>(Computed<T> computed, TArg arg);
}
