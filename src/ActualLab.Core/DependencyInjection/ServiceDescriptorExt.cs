namespace ActualLab.DependencyInjection;

public static class ServiceDescriptorExt
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetImplementationType")]
    private static extern Type? GetImplementationTypeImpl(ServiceDescriptor @this);
#if NET8_0_OR_GREATER
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_implementationFactory")]
    private static extern ref object? ImplementationFactoryImpl(ServiceDescriptor @this);
#else
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_implementationFactory")]
    private static extern ref Func<IServiceProvider, object>? ImplementationFactoryImpl(ServiceDescriptor @this);
#endif

    public static Type? GetImplementationType(this ServiceDescriptor descriptor)
        => GetImplementationTypeImpl(descriptor);

    public static Func<IServiceProvider, object>? GetImplementationFactory(this ServiceDescriptor descriptor)
        => descriptor.ImplementationFactory;

    public static void SetImplementationFactory(
        this ServiceDescriptor descriptor, Func<IServiceProvider, object>? implementationFactory)
        => ImplementationFactoryImpl(descriptor) = implementationFactory;
}
