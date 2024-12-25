namespace ActualLab.DependencyInjection.Internal;

public abstract record ScopedOrSingleton<T>(T Service, IServiceProvider Services)
    where T : class
{
    public sealed record Singleton(T Service, IServiceProvider Services) : ScopedOrSingleton<T>(Service, Services);
    public sealed record Scoped(T Service, IServiceProvider Services) : ScopedOrSingleton<T>(Service, Services);
}
