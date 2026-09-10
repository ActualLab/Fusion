using ActualLab.CommandR.Configuration;
using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion.Operations;

/// <summary>
/// Resolves the <see cref="InvalidationMode"/> of a command handler: its method's
/// <see cref="InvalidationModeAttribute"/>, then its declaring/service type's one,
/// then the app-wide <see cref="DefaultMode"/>.
/// </summary>
public sealed class InvalidationModeResolver
{
    private readonly ConcurrentDictionary<(Type, MethodInfo), InvalidationMode> _cache = new();
    private readonly ConcurrentDictionary<Type, InvalidationMode> _overrides = new();

    public InvalidationMode DefaultMode {
        get;
        set {
            field = value;
            _cache.Clear();
        }
    } = InvalidationMode.Legacy;

    public InvalidationMode Resolve(IMethodCommandHandler handler)
        => Resolve(handler.GetHandlerServiceType(), handler.Method);

    public InvalidationMode Resolve(Type serviceType, MethodInfo method)
        => _cache.GetOrAdd((serviceType, method),
            static (key, self) => self.ResolveUncached(key.Item1, key.Item2),
            this);

    // Configuration may move a service between Local and Replicated - those two share the handler
    // body shape, so the choice is a deployment one. Anything else must be changed in code.
    public void Override(Type serviceType, InvalidationMode mode)
    {
        if (mode is not (InvalidationMode.Local or InvalidationMode.Replicated))
            throw Errors.InvalidationModeOverrideIsNotAllowed(serviceType, mode, mode);

        _overrides[serviceType] = mode;
        _cache.Clear();
    }

    // Private methods

    private InvalidationMode ResolveUncached(Type serviceType, MethodInfo method)
    {
        var mode = GetDeclaredMode(serviceType, method) ?? DefaultMode;
        if (!TryGetOverride(serviceType, method, out var overrideMode))
            return mode;
        if (mode is not (InvalidationMode.Local or InvalidationMode.Replicated))
            throw Errors.InvalidationModeOverrideIsNotAllowed(serviceType, mode, overrideMode);

        return overrideMode;
    }

    private static InvalidationMode? GetDeclaredMode(Type serviceType, MethodInfo method)
    {
        if (method.GetCustomAttribute<InvalidationModeAttribute>(true) is { } methodAttribute)
            return methodAttribute.Mode;
        if (method.DeclaringType?.GetCustomAttribute<InvalidationModeAttribute>(true) is { } typeAttribute)
            return typeAttribute.Mode;
        if (serviceType.GetCustomAttribute<InvalidationModeAttribute>(true) is { } serviceAttribute)
            return serviceAttribute.Mode;

        return null;
    }

    private bool TryGetOverride(Type serviceType, MethodInfo method, out InvalidationMode mode)
    {
        if (_overrides.TryGetValue(serviceType, out mode))
            return true;
        if (method.DeclaringType is { } declaringType && _overrides.TryGetValue(declaringType, out mode))
            return true;

        mode = default;
        return false;
    }
}
