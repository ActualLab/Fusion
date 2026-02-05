namespace ActualLab.DependencyInjection.Internal;

/// <summary>
/// A wrapper used to differentiate between scoped and singleton service registrations
/// in the DI container for the same underlying type.
/// </summary>
public abstract record ScopedOrSingleton<T>(IServiceProvider Services, T Value)
    where T : class
{
    /// <summary>
    /// Holds a singleton-lifetime value.
    /// </summary>
    public sealed record Singleton(IServiceProvider Services, T Value)
        : ScopedOrSingleton<T>(Services, Value);

    /// <summary>
    /// Holds a scoped-lifetime value.
    /// </summary>
    public sealed record Scoped(IServiceProvider Services, T Value)
        : ScopedOrSingleton<T>(Services, Value);
}
