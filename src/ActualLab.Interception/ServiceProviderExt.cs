using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Interception;

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
#pragma warning disable IL2072
        var proxy = (TBaseType)services.Activate(proxyType);
#pragma warning restore IL2072
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }

    public static IProxy ActivateProxy(
        this IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type baseType,
        Interceptor interceptor, object? proxyTarget = null, bool initialize = true)
    {
        var proxyType = Proxies.GetProxyType(baseType);
#pragma warning disable IL2072
        var proxy = (IProxy)services.Activate(proxyType);
#pragma warning restore IL2072
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }

    // GetTypeViewFactory

    public static ITypeViewFactory TypeViewFactory(this IServiceProvider services)
        => services.GetService<ITypeViewFactory>() ?? Interception.TypeViewFactory.Default;

    public static TypeViewFactory<TView> TypeViewFactory<TView>(this IServiceProvider services)
        where TView : class
        => services.TypeViewFactory().For<TView>();
}
