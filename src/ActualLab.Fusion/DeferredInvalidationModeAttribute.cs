namespace ActualLab.Fusion;

/// <summary>
/// Declares the <see cref="DeferredInvalidationMode"/> of a single <c>[CommandHandler]</c> method
/// or of every command handler declared by a compute service type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method)]
public sealed class DeferredInvalidationModeAttribute(DeferredInvalidationMode mode) : Attribute
{
    private static readonly ConcurrentDictionary<(MethodInfo, Type?), DeferredInvalidationModeAttribute?> Cache = new();

    public DeferredInvalidationMode Mode { get; } = mode;

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "We assume all command invalidation handling code is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "We assume all command invalidation handling code is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "We assume all command invalidation handling code is preserved")]
    // implementationType: a command handler declared on an interface runs on the implementation,
    // and that's what decides which modes are even feasible - so it wins over the interface
    public static DeferredInvalidationModeAttribute? Get(MethodInfo method, Type? implementationType = null)
        => Cache.GetOrAdd((method, implementationType),
            static key => {
                var (method, implementationType) = key;
                if (method.GetAttribute<DeferredInvalidationModeAttribute>(true, true) is { } methodAttr)
                    return methodAttr;
                // A handler declared on an interface runs the implementation's method, and that's
                // where a per-method mode is most naturally written. The lookup above can't see it:
                // attribute inheritance runs from an override toward what it overrides, never the
                // other way, so an interface method knows nothing about its implementations.
                if (implementationType is not null
                    && TryGetImplementationMethod(method, implementationType) is { } implementationMethod
                    && implementationMethod.GetAttribute<DeferredInvalidationModeAttribute>(true, true)
                        is { } implementationMethodAttr)
                    return implementationMethodAttr;
                if (implementationType is not null && GetForType(implementationType) is { } implementationAttr)
                    return implementationAttr;

                return method.DeclaringType is { } type ? GetForType(type) : null;
            });

    // Private methods

    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "We assume all command invalidation handling code is preserved")]
    private static DeferredInvalidationModeAttribute? GetForType(Type type)
    {
        if (type.GetCustomAttribute<DeferredInvalidationModeAttribute>(inherit: true) is { } typeAttr)
            return typeAttr;

        // Ordered rather than Type.GetInterfaces(), whose order isn't specified: a service
        // implementing two mode-carrying interfaces must resolve to the same one every time, and
        // the more derived interface is the one that should win
        foreach (var interfaceType in type.GetInterfacesByDependency())
            if (interfaceType.GetCustomAttribute<DeferredInvalidationModeAttribute>(inherit: true) is { } interfaceAttr)
                return interfaceAttr;

        return null;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "We assume all command invalidation handling code is preserved")]
    private static MethodInfo? TryGetImplementationMethod(MethodInfo method, Type implementationType)
    {
        if (method.DeclaringType is not { IsInterface: true } interfaceType)
            return null;

        implementationType = implementationType.NonProxyType();
        if (implementationType.IsInterface || !interfaceType.IsAssignableFrom(implementationType))
            return null;

        var map = implementationType.GetInterfaceMap(interfaceType);
        for (var i = 0; i < map.InterfaceMethods.Length; i++)
            if (map.InterfaceMethods[i] == method)
                return map.TargetMethods[i];

        return null;
    }
}
