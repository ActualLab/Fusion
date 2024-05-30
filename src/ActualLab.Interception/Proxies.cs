using System.Diagnostics.CodeAnalysis;
using Cysharp.Text;
using ActualLab.Interception.Interceptors;
using ActualLab.Interception.Internal;

namespace ActualLab.Interception;

#pragma warning disable IL2026, IL2055, IL2072

public static class Proxies
{
    private static readonly ConcurrentDictionary<Type, Type?> Cache = new();

    public static IProxy CreateInstance(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        Interceptor interceptor, object? proxyTarget = null, bool initialize = true)
    {
        var proxyType = GetProxyType(type);
        var proxy = (IProxy)proxyType.CreateInstance();
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }

    // GetProxyType

    public static Type GetProxyType<TType>()
        where TType : class, IRequiresAsyncProxy
        => GetProxyType(typeof(TType));

    public static Type GetProxyType(Type type)
        => TryGetProxyType(type) ?? throw Errors.NoProxyType(type);

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
    public static Type? TryGetProxyType(Type type)
        => Cache.GetOrAdd(type, static type1 => {
            if (type1.IsConstructedGenericType) {
                var genericType = TryGetProxyType(type1.GetGenericTypeDefinition());
                return genericType?.MakeGenericType(type1.GenericTypeArguments);
            }

            var name = type1.Name;
            var namePrefix = name;
            var nameSuffix = "";
            if (type1.IsGenericTypeDefinition) {
                var backTrickIndex = name.IndexOf('`', StringComparison.Ordinal);
                if (backTrickIndex < 0)
                    return null; // Weird case, shouldn't happen

                namePrefix = name[..backTrickIndex];
                nameSuffix = name[backTrickIndex..];
            }
            var proxyTypeName = ZString.Concat(
                type1.Namespace,
                type1.Namespace.IsNullOrEmpty() ? "" : ".",
                "ActualLabProxies.",
                namePrefix,
                "Proxy",
                nameSuffix);
            return type1.Assembly.GetType(proxyTypeName);
        });
}
