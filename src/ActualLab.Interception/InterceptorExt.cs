using ActualLab.OS;

namespace ActualLab.Interception;

public static class InterceptorExt
{
    private static readonly MethodInfo InterceptUntypedMethod = typeof(InterceptorExt)
        .GetMethod(nameof(InterceptUntypedImpl), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo ProceedUntypedMethod = typeof(InterceptorExt)
        .GetMethod(nameof(ProceedUntypedImpl), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly ConcurrentDictionary<Type, Func<Interceptor, Invocation, object?>> InterceptUntypedCache
        = new(HardwareInfo.GetProcessorCountPo2Factor(4), 256);
    private static readonly ConcurrentDictionary<Type, Func<Interceptor, Invocation, object?>> ProceedUntypedCache
        = new(HardwareInfo.GetProcessorCountPo2Factor(4), 256);

    public static object? InterceptUntyped(this Interceptor interceptor, Invocation invocation)
        => InterceptUntypedCache.GetOrAdd(invocation.Method.ReturnType,
            static returnType => returnType == typeof(void)
                ? (interceptor, invocation) => {
                    interceptor.Intercept(invocation);
                    return null;
                }
                : (Func<Interceptor, Invocation, object?>)InterceptUntypedMethod
                    .MakeGenericMethod(returnType)
                    .CreateDelegate(typeof(Func<Interceptor, Invocation, object?>))
        ).Invoke(interceptor, invocation);

    public static object? ProceedUntyped(this Interceptor interceptor, Invocation invocation)
        => ProceedUntypedCache.GetOrAdd(invocation.Method.ReturnType,
            static returnType => returnType == typeof(void)
                ? (interceptor, invocation) => {
                    interceptor.Proceed(invocation);
                    return null;
                }
                : (Func<Interceptor, Invocation, object?>)ProceedUntypedMethod
                    .MakeGenericMethod(returnType)
                    .CreateDelegate(typeof(Func<Interceptor, Invocation, object?>))
        ).Invoke(interceptor, invocation);

    // Private methods

    private static object? InterceptUntypedImpl<TResult>(Interceptor interceptor, Invocation invocation)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => interceptor.Intercept<TResult>(invocation);

    private static object? ProceedUntypedImpl<TResult>(Interceptor interceptor, Invocation invocation)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => interceptor.Proceed<TResult>(invocation);
}
