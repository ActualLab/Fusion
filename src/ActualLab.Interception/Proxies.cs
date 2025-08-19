using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception.Interceptors;
using ActualLab.Interception.Internal;
using ActualLab.OS;
using ActualLab.Trimming;

namespace ActualLab.Interception;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume proxy-related code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume proxy-related code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume proxy-related code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "We assume proxy-related code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume proxy-related code is preserved")]
public static class Proxies
{
    internal static readonly ConcurrentDictionary<Type, Type?> Cache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    static Proxies() => CodeKeeper.AddFakeAction(
        static () => {
            CodeKeeper.KeepStatic(typeof(ProxyHelper));

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

            // Build-in interceptors
            CodeKeeper.Keep<TypedFactoryInterceptor>();
            CodeKeeper.Keep<SchedulingInterceptor>();
        });

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

            var name = type.Name;
            var namePrefix = name;
            var nameSuffix = "";
            if (type.IsGenericTypeDefinition) {
                var backTrickIndex = name.IndexOf('`', StringComparison.Ordinal);
                if (backTrickIndex < 0)
                    return null; // Weird case, shouldn't happen

                namePrefix = name[..backTrickIndex];
                nameSuffix = name[backTrickIndex..];
            }
            var proxyTypeName = string.Concat(
                type.Namespace,
                type.Namespace.IsNullOrEmpty() ? "" : ".",
                "ActualLabProxies.",
                namePrefix,
                "Proxy",
                nameSuffix);
            var proxyType = type.Assembly.GetType(proxyTypeName);
            return proxyType;
        });
}
