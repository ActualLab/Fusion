using ActualLab.Interception.Internal;

namespace ActualLab.Interception;

public class Interceptor
{
    public virtual void BindTo(IProxy proxy, object? proxyTarget = null)
    {
        proxy.SetInterceptor(this);
        if (proxyTarget != null)
            proxy.RequireProxy<InterfaceProxy>().ProxyTarget = proxyTarget;
        // ReSharper disable once SuspiciousTypeConversion.Global
        if (proxy is INotifyInitialized notifyInitialized)
            notifyInitialized.Initialized();
    }

    public virtual void Intercept(Invocation invocation)
        => invocation.Intercepted();

    public virtual TResult Intercept<TResult>(Invocation invocation)
        => invocation.Intercepted<TResult>();
}
