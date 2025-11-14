using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Interception;

[UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume proxy-related code is preserved")]
public static class ServiceProviderExt
{
    extension(IServiceProvider services)
    {
        public IProxy ActivateProxy([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type baseType,
            Interceptor interceptor,
            bool initialize = true)
        {
            var proxyType = Proxies.GetProxyType(baseType);
            var proxy = (IProxy)services.CreateInstance(proxyType);
            interceptor.BindTo(proxy, proxyTarget: null, initialize);
            return proxy;
        }

        public IProxy ActivateProxy([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type baseType,
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
}
