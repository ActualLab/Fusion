namespace ActualLab.DependencyInjection.Internal;

public abstract record ScopedOrSingleton<T>(IServiceProvider Services, T Value)
    where T : class
{
    public sealed record Singleton(IServiceProvider Services, T Value)
        : ScopedOrSingleton<T>(Services, Value);
    public sealed record Scoped(IServiceProvider Services, T Value)
        : ScopedOrSingleton<T>(Services, Value);
}
