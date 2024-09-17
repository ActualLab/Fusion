using System.Collections.Concurrent;
using Castle.DynamicProxy;
using IInvocation = Castle.DynamicProxy.IInvocation;

namespace ActualLab.Tests.Interception.Interceptors;

public class CastlePassThroughInterceptor : IInterceptor
{
    private static readonly object VoidTag = new();
    private static readonly ConcurrentDictionary<Type, object?> ResultCache = new();

    public void Intercept(IInvocation invocation)
        => invocation.Proceed();
}
