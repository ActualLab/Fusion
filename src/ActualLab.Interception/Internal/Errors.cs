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
}
