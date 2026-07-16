using ActualLab.Interception.Interceptors;
using ActualLab.Interception.Internal;
using ActualLab.OS;
using ActualLab.Trimming;

namespace ActualLab.Interception;

/// <summary>
/// Provides methods for creating proxy instances and resolving
/// generated proxy types for <see cref="IRequiresAsyncProxy"/> base types.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume proxy-related code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume proxy-related code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume proxy-related code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "We assume proxy-related code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume proxy-related code is preserved")]
public static class Proxies
{
    internal static readonly ConcurrentDictionary<Type, Type?> Cache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    static Proxies()
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        CodeKeeper.Keep(typeof(ProxyHelper));

        // ArgumentList
        CodeKeeper.Keep<ArgumentListType>();
        CodeKeeper.Keep<ArgumentList0>();
        CodeKeeper.Keep<ArgumentListS1>();
        CodeKeeper.Keep<ArgumentListS2>();
        CodeKeeper.Keep<ArgumentListS3>();
        CodeKeeper.Keep<ArgumentListS4>();
        CodeKeeper.Keep<ArgumentListS5>();
        CodeKeeper.Keep<ArgumentListS6>();
        CodeKeeper.Keep<ArgumentListS7>();
        CodeKeeper.Keep<ArgumentListS8>();
        CodeKeeper.Keep<ArgumentListS9>();
        CodeKeeper.Keep<ArgumentListS10>();

        // Invocation, interceptor, proxies
        CodeKeeper.Keep<MethodDef>();
        CodeKeeper.Keep<Invocation>();
        CodeKeeper.Keep<Interceptor>();
        CodeKeeper.Keep<InterfaceProxy>();

        // Built-in interceptors
        CodeKeeper.Keep<TypedFactoryInterceptor>();
        CodeKeeper.Keep<SchedulingInterceptor>();
    }

    public static IProxy New(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type baseType,
        Interceptor interceptor, object? proxyTarget = null, bool initialize = true)
    {
        var proxyType = GetProxyType(baseType);
        var proxy = (IProxy)proxyType.CreateInstance();
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }

    // GetProxyType

    public static Type GetProxyType<TBaseType>()
        where TBaseType : class, IRequiresAsyncProxy
        => GetProxyType(typeof(TBaseType));

    public static Type GetProxyType(Type baseType)
        => TryGetProxyType(baseType) ?? throw Errors.NoProxyType(baseType);

    public static Type? TryGetProxyType(Type baseType)
        => Cache.GetOrAdd(baseType, static type => {
            if (type.IsConstructedGenericType) {
                var genericProxyType = TryGetProxyType(type.GetGenericTypeDefinition());
                return genericProxyType?.MakeGenericType(type.GenericTypeArguments);
            }

            var typeNames = new Stack<string>();
            var isProxyType = true;
            for (var currentType = type; currentType is not null; currentType = currentType.DeclaringType) {
                var name = currentType.Name;
                if (isProxyType) {
                    var backtickIndex = name.IndexOf('`', StringComparison.Ordinal);
                    name = backtickIndex < 0
                        ? name + "Proxy"
                        : name[..backtickIndex] + "Proxy" + name[backtickIndex..];
                    isProxyType = false;
                }
                typeNames.Push(name);
            }
            var proxyTypeName = string.Concat(
                type.Namespace,
                type.Namespace.IsNullOrEmpty() ? "" : ".",
                "ActualLabProxies.",
                string.Join("+", typeNames));
            var proxyType = type.Assembly.GetType(proxyTypeName);
            return proxyType;
        });
}
