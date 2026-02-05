using ActualLab.Interception.Internal;
using ActualLab.OS;

namespace ActualLab.Interception;

/// <summary>
/// Extension methods for <see cref="Invocation"/>.
/// </summary>
public static class InvocationExt
{
    private delegate object? InterceptedUntypedFunc(in Invocation invocation);

    private static readonly MethodInfo InvokeInterceptedUntypedImplMethod = typeof(InvocationExt)
        .GetMethod(nameof(InvokeInterceptedUntypedImpl), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly ConcurrentDictionary<Type, InterceptedUntypedFunc> InterceptedUntypedCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume InvokeInterceptedUntypedImpl method is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume InvokeInterceptedUntypedImpl method is preserved")]
    public static object? InvokeInterceptedUntyped(in this Invocation invocation)
        => InterceptedUntypedCache.GetOrAdd(invocation.Method.ReturnType,
            static returnType => returnType == typeof(void)
                ? (in Invocation invocation) => {
                    invocation.InvokeIntercepted();
                    return null;
                }
                : (InterceptedUntypedFunc)InvokeInterceptedUntypedImplMethod
                    .MakeGenericMethod(returnType)
                    .CreateDelegate(typeof(InterceptedUntypedFunc))
        ).Invoke(invocation);

    // Private methods

    private static object? InvokeInterceptedUntypedImpl<TResult>(in Invocation invocation)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => invocation.InterceptedDelegate is Func<ArgumentList, TResult> func
            ? func.Invoke(invocation.Arguments)
            : throw Errors.InvalidInterceptedDelegate();
}
