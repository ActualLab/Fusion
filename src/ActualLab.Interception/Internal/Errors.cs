namespace ActualLab.Interception.Internal;

/// <summary>
/// Factory methods for interception-related exceptions.
/// </summary>
public static class Errors
{
    public static Exception NoProxyType(Type type)
        => new InvalidOperationException(
            $"Type '{type.GetName()}' doesn't have a proxy type generated for it. Please verify that 'ActualLab.Generators' package is referenced.");

    public static Exception InvalidProxyType(Type? type, Type expectedType)
        => new InvalidOperationException(
            $"A proxy of type '{type?.GetName() ?? "null"}' is expected to implement '{expectedType.GetName()}'.");

    public static Exception InvalidInterceptedDelegate()
        => new InvalidOperationException(
            $"{nameof(Invocation)}.{nameof(Invocation.InterceptedDelegate)} is null or doesn't have an expected type.");

    public static Exception NoInterfaceProxyTarget()
        => new InvalidOperationException(
            $"{nameof(Invocation)}.{nameof(Invocation.InterfaceProxyTarget)} is null.");

    public static Exception SyncMethodResultTaskMustBeCompleted()
        => new InvalidOperationException(
            "The intercepted method is synchronous, but the task wrapping its result isn't completed yet.");

    // Proxy exceptions

    public static Exception NoInterceptor()
        => new InvalidOperationException("This proxy has no interceptor - you must call SetInterceptor method first.");

    public static Exception InterceptorIsAlreadyBound()
        => new InvalidOperationException(
            "This proxy already has an interceptor: it can be bound just once, right after the proxy construction.");

    public static Exception InvalidInterceptorBinding()
        => new ArgumentOutOfRangeException("value",
            "The binding's method table doesn't match this proxy's method table.");

    // Serialization

    public static Exception InvalidItemTypeFormat()
        => new SerializationException("Invalid item type format.");
    public static Exception CannotDeserializeUnexpectedArgumentType(Type expectedType, Type actualType)
        => new SerializationException($"Cannot deserialize unexpected argument type: " +
            $"expected '{expectedType.GetName()}' (exact match), got '{actualType.GetName()}'.");
    public static Exception CannotDeserializeUnexpectedPolymorphicArgumentType(Type expectedType, Type actualType)
        => new SerializationException($"Cannot deserialize polymorphic argument type: " +
            $"expected '{expectedType.GetName()}' or its descendant, got '{actualType.GetName()}'.");
    public static Exception PolymorphicObjectButNonPolymorphicSerializer(Type expectedType, Type actualType)
        => new SerializationException(
            $"An object of type '{actualType.GetName()}' is polymorphic descendant of '{expectedType.GetName()}', "
            + $"but polymorphic serialization is not allowed by the selected serialization format.");
}
