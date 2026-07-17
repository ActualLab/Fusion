using ActualLab.Interception.Internal;
using ActualLab.OS;

namespace ActualLab.Interception;

/// <summary>
/// Extension methods for <see cref="Invocation"/>.
/// </summary>
public static class InvocationExt
{
    private static readonly MethodInfo InvokeInterceptedUntypedImplMethod = typeof(InvocationExt)
        .GetMethod(nameof(InvokeInterceptedUntypedImpl), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly ConcurrentDictionary<Type, Func<Invocation, object?>> InterceptedUntypedCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static object? InvokeInterceptedUntyped(in this Invocation invocation)
        => GetInterceptedUntypedInvoker(invocation.Method.ReturnType).Invoke(invocation);

    // The returned invoker is also cached as the per-slot handler
    // for methods their interceptor leaves unhandled.
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume InvokeInterceptedUntypedImpl method is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume InvokeInterceptedUntypedImpl method is preserved")]
    public static Func<Invocation, object?> GetInterceptedUntypedInvoker(Type returnType)
        => InterceptedUntypedCache.GetOrAdd(returnType,
            static returnType1 => returnType1 == typeof(void)
                ? invocation => {
                    invocation.InvokeIntercepted();
                    return null;
                }
                : (Func<Invocation, object?>)InvokeInterceptedUntypedImplMethod
                    .MakeGenericMethod(returnType1)
                    .CreateDelegate(typeof(Func<Invocation, object?>))
        );

    // Private methods

    private static object? InvokeInterceptedUntypedImpl<TResult>(Invocation invocation)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => invocation.InterceptedDelegate is Func<ArgumentList, TResult> func
            ? func.Invoke(invocation.Arguments)
            : throw Errors.InvalidInterceptedDelegate();
}
