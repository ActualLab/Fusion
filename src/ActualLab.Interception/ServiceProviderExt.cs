namespace ActualLab.Interception;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/> related to proxy activation.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume proxy-related code is preserved")]
public static class ServiceProviderExt
{
    public static IProxy ActivateProxy(this IServiceProvider services, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type baseType,
        Interceptor interceptor,
        bool initialize = true)
    {
        var proxyType = Proxies.GetProxyType(baseType);
        var proxy = (IProxy)services.CreateInstance(proxyType);
        interceptor.BindTo(proxy, proxyTarget: null, initialize);
        return proxy;
    }

    public static IProxy ActivateProxy(this IServiceProvider services, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type baseType,
        Interceptor interceptor,
        object? proxyTarget,
        bool initialize = true)
    {
        var proxyType = Proxies.GetProxyType(baseType);
        var proxy = (IProxy)services.CreateInstance(proxyType);
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }
}
