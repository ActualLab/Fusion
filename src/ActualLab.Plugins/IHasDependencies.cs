namespace ActualLab.Plugins;

public interface IHasDependencies
{
    public IEnumerable<Type> Dependencies { get; }
}
