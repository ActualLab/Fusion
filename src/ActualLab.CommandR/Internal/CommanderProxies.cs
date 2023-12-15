using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Interception;
using ActualLab.Interception;

namespace ActualLab.CommandR.Internal;

public static class CommanderProxies
{
    public static object NewServiceProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType)
    {
        // We should try to validate it here because if the type doesn't
        // have any virtual methods (which might be a mistake), no calls
        // will be intercepted, so no error will be thrown later.

        var interceptor = services.GetRequiredService<CommandServiceInterceptor>();
        interceptor.ValidateType(implementationType);
        var proxy = services.ActivateProxy(implementationType, interceptor);
        return proxy;
    }
}
