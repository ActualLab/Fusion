using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Castle.DynamicProxy;
using IInvocation = Castle.DynamicProxy.IInvocation;

namespace ActualLab.Tests.Interception.Interceptors;

public class CastleDefaultResultInterceptor : IInterceptor
{
    private static readonly object VoidTag = new();
    private static readonly ConcurrentDictionary<Type, object?> ResultCache = new();

    [UnconditionalSuppressMessage("Trimming", "IL2062", Justification = "We assume test code is fully preserved")]
    public void Intercept(IInvocation invocation)
    {
        var result = ResultCache.GetOrAdd(invocation.Method.ReturnType, static t => {
            if (!t.IsClass)
                return t == typeof(void) ? VoidTag : Activator.CreateInstance(t);

            return typeof(Task).IsAssignableFrom(t)
                ? t.IsGenericType
                    ? TaskExt.FromDefaultResult(t.GenericTypeArguments[0])
                    : Task.CompletedTask
                : null;
        });
        if (!ReferenceEquals(result, VoidTag))
            invocation.ReturnValue = result;
    }
}
