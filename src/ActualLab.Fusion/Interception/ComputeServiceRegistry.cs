using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

/// <summary>
/// Maps compute service implementation types to the types they are registered as in DI,
/// so a recorded <see cref="ActualLab.CommandR.Operations.InvalidationCall"/> can be resolved
/// back to a service instance and a method on any host.
/// </summary>
public sealed class ComputeServiceRegistry
{
    private const BindingFlags MethodBindingFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private readonly ConcurrentDictionary<Type, Type> _serviceTypes = new();
    private readonly ConcurrentDictionary<(Type, string, int), MethodInfo?> _methods = new();

    public void Register(Type serviceType, Type implementationType)
        => _serviceTypes[implementationType] = serviceType;

    public Type GetServiceType(Type implementationType)
        => _serviceTypes.GetValueOrDefault(implementationType.NonProxyType()) ?? implementationType;

    public MethodInfo? TryGetMethod(Type implementationType, string methodName, int argumentCount)
        => _methods.GetOrAdd((implementationType, methodName, argumentCount),
            static key => FindMethod(key.Item1, key.Item2, key.Item3));

    // Private methods

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "We assume compute service code is preserved")]
    private static MethodInfo? FindMethod(Type type, string methodName, int argumentCount)
    {
        // argumentCount excludes the trailing CancellationToken, which most compute methods have
        MethodInfo? result = null;
        foreach (var method in type.GetMethods(MethodBindingFlags)) {
            if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                continue;

            var parameters = method.GetParameters();
            var isMatch = parameters.Length == argumentCount
                || (parameters.Length == argumentCount + 1
                    && parameters[^1].ParameterType == typeof(CancellationToken));
            if (!isMatch)
                continue;
            if (result is not null)
                return null; // Ambiguous overload - the caller logs & skips it

            result = method;
        }
        return result;
    }
}
