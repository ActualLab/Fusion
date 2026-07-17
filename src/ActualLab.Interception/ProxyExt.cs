namespace ActualLab.Interception;

/// <summary>
/// Extension methods for <see cref="IRequiresAsyncProxy"/>.
/// </summary>
public static class ProxyExt
{
    public static IServiceProvider GetServices(IRequiresAsyncProxy proxy)
    {
        var interceptor = ((IProxy)proxy).Binding.Interceptor;
        return ((IHasServices)interceptor).Services;
    }
}
