using ActualLab.Trimming;

namespace ActualLab.Interception.Trimming;

public class ProxyCodeKeeper : CodeKeeper
{
    private readonly MethodDefCodeKeeper _methodDefCodeKeeper = Get<MethodDefCodeKeeper>();

    public virtual void KeepProxy<TBase, TProxy>()
        where TBase : IRequiresAsyncProxy
        where TProxy : IProxy
    {
        if (AlwaysTrue)
            return;

        Keep<TBase>();
        Keep<TProxy>();
    }

    public void KeepSyncMethod<TResult>()
        => KeepMethod<TResult, TResult>();

    public void KeepSyncMethod<TResult, T0>()
        => KeepMethod<TResult, TResult, T0>();

    public void KeepSyncMethod<TResult, T0, T1>()
        => KeepMethod<TResult, TResult, T0, T1>();

    public void KeepSyncMethod<TResult, T0, T1, T2>()
        => KeepMethod<TResult, TResult, T0, T1, T2>();

    public void KeepSyncMethod<TResult, T0, T1, T2, T3>()
        => KeepMethod<TResult, TResult, T0, T1, T2, T3>();

    public void KeepSyncMethod<TResult, T0, T1, T2, T3, T4>()
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4>();

    public void KeepSyncMethod<TResult, T0, T1, T2, T3, T4, T5>()
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4, T5>();

    public void KeepSyncMethod<TResult, T0, T1, T2, T3, T4, T5, T6>()
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4, T5, T6>();

    public void KeepSyncMethod<TResult, T0, T1, T2, T3, T4, T5, T6, T7>()
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4, T5, T6, T7>();

    public void KeepSyncMethod<TResult, T0, T1, T2, T3, T4, T5, T6, T7, T8>()
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4, T5, T6, T7, T8>();

    public void KeepSyncMethod<TResult, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>()
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>();

    public void KeepSyncMethod<TResult, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>()
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>();

    public void KeepAsyncMethod<TUnwrapped>()
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped>();
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped>();
    }

    public void KeepAsyncMethod<TUnwrapped, T0>()
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0>();
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0>();
    }

    public void KeepAsyncMethod<TUnwrapped, T0, T1>()
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1>();
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1>();
    }

    public void KeepAsyncMethod<TUnwrapped, T0, T1, T2>()
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2>();
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2>();
    }

    public void KeepAsyncMethod<TUnwrapped, T0, T1, T2, T3>()
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3>();
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3>();
    }

    public void KeepAsyncMethod<TUnwrapped, T0, T1, T2, T3, T4>()
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4>();
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4>();
    }

    public void KeepAsyncMethod<TUnwrapped, T0, T1, T2, T3, T4, T5>()
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5>();
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5>();
    }

    public void KeepAsyncMethod<TUnwrapped, T0, T1, T2, T3, T4, T5, T6>()
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6>();
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6>();
    }

    public void KeepAsyncMethod<TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7>()
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7>();
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7>();
    }

    public void KeepAsyncMethod<TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8>()
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8>();
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8>();
    }

    public void KeepAsyncMethod<TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>()
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>();
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>();
    }

    public void KeepAsyncMethod<TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>()
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>();
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>();
    }

    public virtual void KeepMethod<TResult, TUnwrapped>()
        => KeepMethodResult<TResult, TUnwrapped>();

    public virtual void KeepMethod<TResult, TUnwrapped, T0>()
    {
        KeepMethodResult<TResult, TUnwrapped>();
        KeepMethodArgument<T0>();
    }

    public virtual void KeepMethod<TResult, TUnwrapped, T0, T1>()
    {
        KeepMethod<TResult, TUnwrapped, T0>();
        KeepMethodArgument<T1>();
    }

    public virtual void KeepMethod<TResult, TUnwrapped, T0, T1, T2>()
    {
        KeepMethod<TResult, TUnwrapped, T0, T1>();
        KeepMethodArgument<T2>();
    }

    public virtual void KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3>()
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2>();
        KeepMethodArgument<T3>();
    }

    public virtual void KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4>()
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3>();
        KeepMethodArgument<T4>();
    }

    public virtual void KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5>()
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4>();
        KeepMethodArgument<T5>();
    }

    public virtual void KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5, T6>()
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5>();
        KeepMethodArgument<T6>();
    }

    public virtual void KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7>()
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5, T6>();
        KeepMethodArgument<T7>();
    }

    public virtual void KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8>()
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7>();
        KeepMethodArgument<T8>();
    }

    public virtual void KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>()
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8>();
        KeepMethodArgument<T9>();
    }

    public virtual void KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>()
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>();
        KeepMethodArgument<T10>();
    }

    public virtual void KeepMethodArgument<TArg>()
    {
        if (AlwaysFalse)
            return;

        Keep<TArg>();
        KeepArgumentListArgument<TArg>();
    }

    public virtual void KeepMethodResult<TResult, TUnwrapped>()
    {
        if (AlwaysTrue)
            return;

        _methodDefCodeKeeper.KeepCodeForResult<TResult, TUnwrapped>();
        Keep<Interceptor>().KeepCodeForResult<TResult, TUnwrapped>();
    }

    public virtual void KeepArgumentListArgument<TArg>()
        => Get<ArgumentListCodeKeeper>().KeepArgumentListArgument<TArg>();
}
