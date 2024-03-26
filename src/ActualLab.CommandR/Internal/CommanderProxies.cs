using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Interception;
using ActualLab.Interception;

namespace ActualLab.CommandR.Internal;

public static class CommanderProxies
{
    public static object NewProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        bool initialize = true)
    {
        var interceptor = services.GetRequiredService<CommandServiceInterceptor>();
        // We should try to validate it here because if the type doesn't
        // have any virtual methods (which might be a mistake), no calls
        // will be intercepted, so no error will be thrown later.
        interceptor.ValidateType(implementationType);
        var proxy = services.ActivateProxy(implementationType, interceptor, null, initialize);
        return proxy;
    }
}
