using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception.Interceptors;
using Cysharp.Text;
using ActualLab.Interception.Internal;
using ActualLab.Trimming;

namespace ActualLab.Interception;

#pragma warning disable IL2026, IL2055, IL2072

public static class Proxies
{
    internal static readonly ConcurrentDictionary<Type, Type?> Cache = new();

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
            CodeKeeper.Keep<TypeViewInterceptor>();
            CodeKeeper.Keep<TypedFactoryInterceptor>();
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
            var proxyTypeName = ZString.Concat(
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
