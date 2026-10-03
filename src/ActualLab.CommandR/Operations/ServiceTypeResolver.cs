namespace ActualLab.CommandR.Operations;

/// <summary>
/// Maps a service implementation type to the type it's registered as in the container,
/// and back.
/// </summary>
public sealed class ServiceTypeResolver
{
    private readonly ConcurrentDictionary<Type, Type> _serviceTypes = new();
    private readonly ConcurrentDictionary<Type, Type> _implementationTypes = new();

    public void Register(Type serviceType, Type implementationType)
    {
        _serviceTypes[implementationType] = serviceType;
        _implementationTypes[serviceType] = implementationType;
    }

    public void RegisterAlias(Type serviceType, Type implementationType)
        => _implementationTypes[serviceType] = implementationType;

    // TryResolveXxx

    public Type? TryResolveServiceType(Type implementationType)
        => _serviceTypes.GetValueOrDefault(implementationType.NonProxyType());

    public Type? TryResolveImplementationType(Type serviceType)
        => _implementationTypes.GetValueOrDefault(serviceType);
}
