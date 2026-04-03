using ActualLab.Caching;
using ActualLab.Trimming;

namespace ActualLab.Interception.Trimming;

/// <summary>
/// Retains proxy-related code and metadata needed for .NET trimming scenarios.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public static class ProxyCodeKeeper
{
    public static IExtension? Extension { get; set; }

    static ProxyCodeKeeper()
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        CodeKeeper.Keep(typeof(Proxies));
        CodeKeeper.Keep<MethodDef>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepProxy<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TBase,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProxy>()
        where TBase : IRequiresAsyncProxy
        where TProxy : IProxy
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        CodeKeeper.Keep<TBase>();
        CodeKeeper.Keep<TProxy>();
        Extension?.KeepProxy<TBase, TProxy>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepSyncMethod<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult>(string name = "")
        => KeepMethod<TResult, TResult>(name);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0>(string name = "")
        => KeepMethod<TResult, TResult, T0>(name);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1>(name);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1, T2>(name);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1, T2, T3>(name);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4>(name);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4, T5>(name);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepSyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6>(string name = "")
        => KeepMethod<TResult, TResult, T0, T1, T2, T3, T4, T5, T6>(name);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepSyncMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepSyncMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepSyncMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepSyncMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped>(name);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0>(name);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1>(name);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2>(name);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepAsyncMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>(string name = "")
    {
        KeepMethod<Task<TUnwrapped>, TUnwrapped, T0, T1, T2, T3>(name);
        KeepMethod<ValueTask<TUnwrapped>, TUnwrapped, T0, T1, T2, T3>(name);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepAsyncMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepAsyncMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepAsyncMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepAsyncMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepAsyncMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepAsyncMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepAsyncMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(string name = "")
        => KeepMethodResult<TResult, TUnwrapped>(name);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0>(string name = "")
    {
        KeepMethod<TResult, TUnwrapped>(name);
        KeepMethodArgument<T0>(name, 0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>(string name = "")
    {
        KeepMethod<TResult, TUnwrapped, T0>(name);
        KeepMethodArgument<T1>(name, 1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepMethod<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T0,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>(string name = "")
    {
        KeepMethod<TResult, TUnwrapped, T0, T1>(name);
        KeepMethodArgument<T2>(name, 2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepMethod<
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepMethodArgument<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TArg>(
        string name = "", int index = -1)
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        CodeKeeper.Keep<TArg>();
        ArgumentListCodeKeeper.KeepArgumentListArgument<TArg>();
        Extension?.KeepMethodArgument<TArg>(name, index);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepMethodResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(string name = "")
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        // Generic factories
        CodeKeeper.Keep<IGenericInstanceFactory<TUnwrapped>>();
        CodeKeeper.Keep<Result.NewErrorFactory<TUnwrapped>>();
        CodeKeeper.Keep<TaskExt.FromExceptionFactory<TUnwrapped>>();
        CodeKeeper.Keep<TaskExt.FromCancelledTaskFactory<TUnwrapped>>();
        CodeKeeper.Keep<TaskExt.ToTypedValueTaskFactory<TUnwrapped>>();
        CodeKeeper.Keep<TaskExt.ToTypedResultSynchronouslyFactory<TUnwrapped>>();
        CodeKeeper.Keep<TaskExt.ToObjectValueTaskFactory<TUnwrapped>>();
        CodeKeeper.Keep<TaskExt.ToUntypedResultSynchronouslyFactory<TUnwrapped>>();
        CodeKeeper.Keep<TaskExt.GetResultAsObjectSynchronouslyFactory<TUnwrapped>>();

        // TResult, TUnwrapped, Result<...> code
        CodeKeeper.Keep<TResult>();
        CodeKeeper.Keep<TUnwrapped>();
        CodeKeeper.Keep<Task<TUnwrapped>>();
        CodeKeeper.Keep<ValueTask<TUnwrapped>>();
        CodeKeeper.Keep<Result>();
        CodeKeeper.Keep<Result<TUnwrapped>>();
        CodeKeeper.Keep<Result<Task<TUnwrapped>>>();
        CodeKeeper.Keep<Result<ValueTask<TUnwrapped>>>();

        // MethodDef: generic methods
        var methodDef = default(MethodDef)!;
        methodDef.WrapResult<TUnwrapped>(default!);
        methodDef.WrapAsyncInvokerResult<TUnwrapped>(default!);
        methodDef.WrapResultOfAsyncMethod<TUnwrapped>(default!);
        methodDef.WrapAsyncInvokerResultOfAsyncMethod<TUnwrapped>(default!);
        methodDef.SelectAsyncInvoker<TUnwrapped>(null!);
        methodDef.GetCachedFunc<TUnwrapped>(null!);

        // MethodDef: invoker factories
        CodeKeeper.Keep<MethodDef.TargetAsyncInvokerFactory<TUnwrapped>>();
        CodeKeeper.Keep<MethodDef.InterceptorAsyncInvokerFactory<TUnwrapped>>();
        CodeKeeper.Keep<MethodDef.InterceptedAsyncInvokerFactory<TUnwrapped>>();
        CodeKeeper.Keep<MethodDef.TargetObjectAsyncInvokerFactory<TUnwrapped>>();
        CodeKeeper.Keep<MethodDef.InterceptorObjectAsyncInvokerFactory<TUnwrapped>>();
        CodeKeeper.Keep<MethodDef.InterceptedObjectAsyncInvokerFactory<TUnwrapped>>();
        CodeKeeper.Keep<MethodDef.UniversalAsyncResultConverterFactory<TUnwrapped>>();

        default(Interceptor)!.CreateTypedHandler<TUnwrapped>(default, null!);
        Extension?.KeepMethodResult<TResult, TUnwrapped>(name);
    }

    // Nested types

    public interface IExtension
    {
        public void KeepProxy<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TBase,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProxy>()
            where TBase : IRequiresAsyncProxy
            where TProxy : IProxy;

        public void KeepMethodArgument<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TArg>(
            string name, int index);

        public void KeepMethodResult<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
            string name);
    }
}
