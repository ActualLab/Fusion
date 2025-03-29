using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Tests.Interception.Interceptors;

public sealed class DefaultResultInterceptor : Interceptor
{
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    public DefaultResultInterceptor(Options settings, IServiceProvider services) : base(settings, services)
    {
        MustInterceptSyncCalls = true;
        MustInterceptAsyncCalls = true;
    }

    protected override Func<Invocation, object?>? CreateTypedHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        Invocation initialInvocation, MethodDef methodDef)
    {
        var defaultResult = methodDef.DefaultResult;
        return _ => defaultResult;
    }
}
