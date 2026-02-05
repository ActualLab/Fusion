namespace ActualLab.DependencyInjection;

/// <summary>
/// Indicates a type that exposes an <see cref="IServiceProvider"/> instance.
/// </summary>
public interface IHasServices
{
    public IServiceProvider Services { get; }
}
