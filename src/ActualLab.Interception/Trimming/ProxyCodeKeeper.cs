using System.Diagnostics.CodeAnalysis;
using ActualLab.Caching;
using ActualLab.Trimming;

namespace ActualLab.Interception.Trimming;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public class ProxyCodeKeeper : CodeKeeper
{
    protected MethodDefCodeKeeper MethodDefCodeKeeper => Get<MethodDefCodeKeeper>();

    static ProxyCodeKeeper()
        => _ = Proxies.Cache;

    public virtual void KeepProxy<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TBase,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProxy>()
        where TBase : IRequiresAsyncProxy
        where TProxy : IProxy
    {
        if (AlwaysTrue)
            return;

        Keep<TBase>();
        Keep<TProxy>();
    }

    public void KeepSyncMethod<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult>(string name = "")
        => KeepMethod<TResult, TResult>(name);

    public void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0>(string name = "")
        => KeepMethod<TResult, TResult, T0>(name);

    public void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1>(name);

    public void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1, T2>(name);

    public void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1, T2, T3>(name);

    public void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4>(name);

    public void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4, T5>(name);

    public void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4, T5, T6>(name);

    public void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4, T5, T6, T7>(name);

    public void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4, T5, T6, T7, T8>(name);

    public void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T9>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(name);

    public void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T9,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T10>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(name);

    public void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped>(name);
    }

    public void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0>(name);
    }

    public void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1>(name);
    }

    public void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2>(name);
    }

    public void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3>(name);
    }

    public void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4>(name);
    }

    public void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5>(name);
    }

    public void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6>(name);
    }

    public void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7>(name);
    }

    public void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8>(name);
    }

    public void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T9>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(name);
    }

    public void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T9,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T10>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(name);
    }

    public virtual void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(string name = "")
        => KeepMethodResult<TResult, TUnwrapped>(name);

    public virtual void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0>(string name = "")
    {
        KeepMethod<TResult, TUnwrapped>(name);
        KeepMethodArgument<T0>(name, 0);
    }

    public virtual void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>(string name = "")
    {
        KeepMethod<TResult, TUnwrapped, T0>(name);
        KeepMethodArgument<T1>(name, 1);
    }

    public virtual void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>(string name = "")
    {
        KeepMethod<TResult, TUnwrapped, T0, T1>(name);
        KeepMethodArgument<T2>(name, 2);
    }

    public virtual void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>(string name = "")
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2>(name);
        KeepMethodArgument<T3>(name, 3);
    }

    public virtual void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4>(string name = "")
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3>(name);
        KeepMethodArgument<T4>(name, 4);
    }

    public virtual void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5>(string name = "")
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4>(name);
        KeepMethodArgument<T5>(name, 5);
    }

    public virtual void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6>(string name = "")
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5>(name);
        KeepMethodArgument<T6>(name, 6);
    }

    public virtual void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7>(string name = "")
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5, T6>(name);
        KeepMethodArgument<T7>(name, 7);
    }

    public virtual void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8>(string name = "")
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7>(name);
        KeepMethodArgument<T8>(name, 8);
    }

    public virtual void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T9>(string name = "")
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8>(name);
        KeepMethodArgument<T9>(name, 9);
    }

    public virtual void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T9,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T10>(string name = "")
    {
        KeepMethod<TResult, TUnwrapped, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(name);
        KeepMethodArgument<T10>(name, 10);
    }

    public virtual void KeepMethodArgument<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TArg>(
        string name = "", int index = -1)
    {
        if (AlwaysFalse)
            return;

        Keep<TArg>();
        KeepArgumentListArgument<TArg>();
    }

    public virtual void KeepMethodResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(string name = "")
    {
        if (AlwaysTrue)
            return;

        Keep<IGenericInstanceFactory<TUnwrapped>>();
        Keep<TaskExt.TaskFromExceptionFactory<TUnwrapped>>();
        Keep<TaskExt.ToTypedValueTaskFactory<TUnwrapped>>();
        Keep<TaskExt.ToUntypedValueTaskFactory<TUnwrapped>>();
        Keep<TaskExt.ToTypedResultSynchronouslyFactory<TUnwrapped>>();

        MethodDefCodeKeeper.KeepCodeForResult<TResult, TUnwrapped>();
        Keep<Interceptor>().KeepCodeForResult<TResult, TUnwrapped>();
    }

    public virtual void KeepArgumentListArgument<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TArg>()
        => Get<ArgumentListCodeKeeper>().KeepArgumentListArgument<TArg>();
}
