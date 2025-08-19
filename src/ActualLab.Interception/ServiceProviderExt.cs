using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Interception;

[UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume proxy-related code is preserved")]
public static class ServiceProviderExt
{
    // ActivateProxy

    public static TBaseType ActivateProxy<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TBaseType>(
        this IServiceProvider services,
        Interceptor interceptor, TBaseType? proxyTarget = null, bool initialize = true)
        where TBaseType : class, IRequiresAsyncProxy
    {
        var proxyType = Proxies.GetProxyType<TBaseType>();
        var proxy = (TBaseType)services.CreateInstance(proxyType);
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }

    public static IProxy ActivateProxy(
        this IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type baseType,
        Interceptor interceptor, object? proxyTarget = null, bool initialize = true)
    {
        var proxyType = Proxies.GetProxyType(baseType);
        var proxy = (IProxy)services.CreateInstance(proxyType);
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }
}
