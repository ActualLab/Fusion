namespace ActualLab.Plugins;

/// <summary>
/// Implement in a plugin to declare its type dependencies for dependency ordering.
/// </summary>
public interface IHasDependencies
{
    public IEnumerable<Type> Dependencies { get; }
}
