using System.Collections.Concurrent;
using Castle.DynamicProxy;
using IInvocation = Castle.DynamicProxy.IInvocation;

namespace ActualLab.Tests.Interception.Interceptors;

public class CastlePassThroughInterceptor : IInterceptor
{
    public void Intercept(IInvocation invocation)
        => invocation.Proceed();
}
