using ActualLab.OS;

namespace ActualLab.Interception;

public static class InterceptorExt
{
    private delegate object? InterceptUntypedFunc(Interceptor interceptor, in Invocation invocation);

    private static readonly MethodInfo InterceptUntypedMethod = typeof(InterceptorExt)
        .GetMethod(nameof(InterceptUntypedImpl), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly ConcurrentDictionary<Type, InterceptUntypedFunc> InterceptUntypedCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume InterceptUntypedImpl method is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume InterceptUntypedImpl method is preserved")]
    public static object? InterceptUntyped(this Interceptor interceptor, in Invocation invocation)
        => InterceptUntypedCache.GetOrAdd(invocation.Method.ReturnType,
            static returnType => returnType == typeof(void)
                ? (Interceptor interceptor, in Invocation invocation) => {
                    interceptor.Intercept(invocation);
                    return null;
                }
                : (InterceptUntypedFunc)InterceptUntypedMethod
                    .MakeGenericMethod(returnType)
                    .CreateDelegate(typeof(InterceptUntypedFunc))
        ).Invoke(interceptor, invocation);

    // Private methods

    private static object? InterceptUntypedImpl<TResult>(Interceptor interceptor, in Invocation invocation)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => interceptor.Intercept<TResult>(invocation);
}
