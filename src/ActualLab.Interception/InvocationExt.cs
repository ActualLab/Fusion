using ActualLab.Interception.Internal;
using ActualLab.OS;

namespace ActualLab.Interception;

public static class InvocationExt
{
    private delegate object? InterceptedUntypedFunc(in Invocation invocation);

    private static readonly MethodInfo InterceptedUntypedMethod = typeof(InvocationExt)
        .GetMethod(nameof(InterceptedUntypedImpl), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly ConcurrentDictionary<Type, InterceptedUntypedFunc> InterceptedUntypedCache
        = new(HardwareInfo.GetProcessorCountPo2Factor(4), 256);

    public static object? InterceptedUntyped(in this Invocation invocation)
        => InterceptedUntypedCache.GetOrAdd(invocation.Method.ReturnType,
            static returnType => returnType == typeof(void)
                ? (in Invocation invocation) => {
                    invocation.Intercepted();
                    return null;
                }
                : (InterceptedUntypedFunc)InterceptedUntypedMethod
                    .MakeGenericMethod(returnType)
                    .CreateDelegate(typeof(InterceptedUntypedFunc))
        ).Invoke(invocation);

    // Private methods

    private static object? InterceptedUntypedImpl<TResult>(in Invocation invocation)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => invocation.InterceptedDelegate is Func<ArgumentList, TResult> func
            ? func.Invoke(invocation.Arguments)
            : throw Errors.InvalidInterceptedDelegate();
}
