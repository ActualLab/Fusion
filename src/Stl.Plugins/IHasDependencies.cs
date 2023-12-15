namespace ActualLab.Plugins;

public interface IHasDependencies
{
    IEnumerable<Type> Dependencies { get; }
}
