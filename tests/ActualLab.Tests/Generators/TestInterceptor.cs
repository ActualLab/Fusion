using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Tests.Generators;

public class TestInterceptor(TestInterceptor.Options settings, IServiceProvider services)
    : Interceptor(settings, services)
{
    public new sealed record Options : Interceptor.Options;

    protected override Func<Invocation, object?>? CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        Invocation initialInvocation, MethodDef methodDef)
        => null;
}
