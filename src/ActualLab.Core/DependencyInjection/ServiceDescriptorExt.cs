using System.Diagnostics.CodeAnalysis;

namespace ActualLab.DependencyInjection;

public static class ServiceDescriptorExt
{
#if USE_UNSAFE_ACCESSORS && NET8_0_OR_GREATER

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetImplementationType")]
    private static extern Type? GetImplementationTypeImpl(ServiceDescriptor @this);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_implementationFactory")]
    private static extern ref object? ImplementationFactoryImpl(ServiceDescriptor @this);

    public static Type? GetImplementationType(this ServiceDescriptor descriptor)
        => GetImplementationTypeImpl(descriptor);

    public static Func<IServiceProvider, object>? GetImplementationFactory(this ServiceDescriptor descriptor)
        => descriptor.ImplementationFactory;

    public static void SetImplementationFactory(
        this ServiceDescriptor descriptor, Func<IServiceProvider, object>? implementationFactory)
        => ImplementationFactoryImpl(descriptor) = implementationFactory;

#else // !USE_UNSAFE_ACCESSORS

    private static readonly Func<ServiceDescriptor, Type?> ImplementationTypeGetter;
    private static readonly Action<ServiceDescriptor, object?> ImplementationFactorySetter;

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ServiceDescriptor))]
    static ServiceDescriptorExt()
    {
        var bfInstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(ServiceDescriptor);
#pragma warning disable IL2026
        ImplementationTypeGetter = (Func<ServiceDescriptor, Type?>)type
            .GetMethod("GetImplementationType", bfInstanceNonPublic)!
            .CreateDelegate(typeof(Func<ServiceDescriptor, Type?>));
        ImplementationFactorySetter = type
            .GetField("_implementationFactory", bfInstanceNonPublic)!
            .GetSetter();
#pragma warning restore IL2026
    }

    public static Type? GetImplementationType(this ServiceDescriptor descriptor)
        => ImplementationTypeGetter.Invoke(descriptor);

    public static Func<IServiceProvider, object>? GetImplementationFactory(this ServiceDescriptor descriptor)
        => descriptor.ImplementationFactory;

    public static void SetImplementationFactory(
        this ServiceDescriptor descriptor, Func<IServiceProvider, object>? implementationFactory)
        => ImplementationFactorySetter.Invoke(descriptor, implementationFactory);

#endif
}
