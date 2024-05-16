namespace ActualLab.DependencyInjection;

public abstract record MixedModeService<T>(T Service, IServiceProvider Services)
    where T : class
{
    public sealed record Singleton(T Service, IServiceProvider Services) : MixedModeService<T>(Service, Services);
    public sealed record Scoped(T Service, IServiceProvider Services) : MixedModeService<T>(Service, Services);
}
