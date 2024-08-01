using System.Diagnostics.CodeAnalysis;
using Cysharp.Text;
using ActualLab.Interception.Interceptors;
using ActualLab.Interception.Internal;

namespace ActualLab.Interception;

#pragma warning disable IL2026, IL2055, IL2072

public static class Proxies
{
    private static readonly ConcurrentDictionary<Type, Type?> Cache = new();

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

#if NET5_0_OR_GREATER
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(InterfaceProxy))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ProxyHelper))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Interceptor))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Interceptor))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(TypeViewInterceptor))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(TypedFactoryInterceptor))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MethodDef))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Invocation))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentList))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Result<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ResultBox<>))]
#endif
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
