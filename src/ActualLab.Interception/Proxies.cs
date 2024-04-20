using System.Diagnostics.CodeAnalysis;
using Cysharp.Text;
using ActualLab.Interception.Interceptors;
using ActualLab.Interception.Internal;

namespace ActualLab.Interception;

public static class Proxies
{
    private static readonly ConcurrentDictionary<Type, Type?> Cache = new();

    // New

#pragma warning disable IL2072
    public static TType New<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TType>
        (Interceptor interceptor, object? proxyTarget = null, bool initialize = true)
        where TType : class, IRequiresAsyncProxy
    {
        var proxy = (TType)GetProxyType(typeof(TType)).CreateInstance();
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }

    public static TType New<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TType, T1>
        (T1 arg1, Interceptor interceptor, object? proxyTarget = null, bool initialize = true)
        where TType : class, IRequiresAsyncProxy
    {
        var proxy = (TType)GetProxyType(typeof(TType)).CreateInstance(arg1);
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }

    public static TType New<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TType, T1, T2>
        (T1 arg1, T2 arg2, Interceptor interceptor, object? proxyTarget = null, bool initialize = true)
        where TType : class, IRequiresAsyncProxy
    {
        var proxy = (TType)GetProxyType(typeof(TType)).CreateInstance(arg1, arg2);
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }

    public static TType New<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TType, T1, T2, T3>
        (T1 arg1, T2 arg2, T3 arg3, Interceptor interceptor, object? proxyTarget = null, bool initialize = true)
        where TType : class, IRequiresAsyncProxy
    {
        var proxy = (TType)GetProxyType(typeof(TType)).CreateInstance(arg1, arg2, arg3);
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }

    public static TType New<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TType, T1, T2, T3, T4>
        (T1 arg1, T2 arg2, T3 arg3, T4 arg4, Interceptor interceptor, object? proxyTarget = null, bool initialize = true)
        where TType : class, IRequiresAsyncProxy
    {
        var proxy = (TType)GetProxyType(typeof(TType)).CreateInstance(arg1, arg2, arg3, arg4);
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }

    public static IProxy New(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        Interceptor interceptor, object? proxyTarget = null, bool initialize = true)
    {
        var proxy = (IProxy)GetProxyType(type).CreateInstance();
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }

    public static IProxy New<T1>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        T1 arg1, Interceptor interceptor, object? proxyTarget = null, bool initialize = true)
    {
        var proxy = (IProxy)GetProxyType(type).CreateInstance(arg1);
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }

    public static IProxy New<T1, T2>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        T1 arg1, T2 arg2, Interceptor interceptor, object? proxyTarget = null, bool initialize = true)
    {
        var proxy = (IProxy)GetProxyType(type).CreateInstance(arg1, arg2);
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }

    public static IProxy New<T1, T2, T3>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        T1 arg1, T2 arg2, T3 arg3, Interceptor interceptor, object? proxyTarget = null, bool initialize = true)
    {
        var proxy = (IProxy)GetProxyType(type).CreateInstance(arg1, arg2, arg3);
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }

    public static IProxy New<T1, T2, T3, T4>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, Interceptor interceptor, object? proxyTarget = null, bool initialize = true)
    {
        var proxy = (IProxy)GetProxyType(type).CreateInstance(arg1, arg2, arg3, arg4);
        interceptor.BindTo(proxy, proxyTarget, initialize);
        return proxy;
    }
#pragma warning restore IL2072

    // GetProxyType

#pragma warning disable IL2026, IL2055
    public static Type GetProxyType<TType>()
        where TType : class, IRequiresAsyncProxy
        => GetProxyType(typeof(TType));

    public static Type GetProxyType(Type type)
        => TryGetProxyType(type) ?? throw Errors.NoProxyType(type);

#if NET5_0_OR_GREATER
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(InterfaceProxy))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ProxyHelper))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Interceptor))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(InterceptorBase))]
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
#pragma warning restore IL2026, IL2055
}
