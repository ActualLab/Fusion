using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Tests.Interception.Interceptors;

public sealed class PassThroughInterceptor : Interceptor
{
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    public PassThroughInterceptor(Options settings, IServiceProvider services) : base(settings, services)
    {
        MustInterceptSyncCalls = false;
        MustInterceptAsyncCalls = false;
    }

    protected override Func<Invocation, object?>? CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        Invocation initialInvocation, MethodDef methodDef)
        => _ => throw new InvalidOperationException("Should never get to this point.");
}
