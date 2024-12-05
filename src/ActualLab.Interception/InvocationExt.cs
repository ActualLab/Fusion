using ActualLab.Interception.Internal;
using ActualLab.OS;

namespace ActualLab.Interception;

public static class InvocationExt
{
    private delegate object? InterceptedUntypedFunc(in Invocation invocation);

    private static readonly MethodInfo InvokeInterceptedUntypedMethod = typeof(InvocationExt)
        .GetMethod(nameof(InvokeInterceptedUntypedImpl), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly ConcurrentDictionary<Type, InterceptedUntypedFunc> InterceptedUntypedCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static object? InvokeInterceptedUntyped(in this Invocation invocation)
        => InterceptedUntypedCache.GetOrAdd(invocation.Method.ReturnType,
            static returnType => returnType == typeof(void)
                ? (in Invocation invocation) => {
                    invocation.InvokeIntercepted();
                    return null;
                }
#pragma warning disable IL2060
                : (InterceptedUntypedFunc)InvokeInterceptedUntypedMethod
                    .MakeGenericMethod(returnType)
                    .CreateDelegate(typeof(InterceptedUntypedFunc))
#pragma warning restore IL2060
        ).Invoke(invocation);

    // Private methods

    private static object? InvokeInterceptedUntypedImpl<TResult>(in Invocation invocation)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => invocation.InterceptedDelegate is Func<ArgumentList, TResult> func
            ? func.Invoke(invocation.Arguments)
            : throw Errors.InvalidInterceptedDelegate();
}
