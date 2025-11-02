namespace ActualLab.Fusion.Internal;

public interface IHasSynchronizationTarget : IComputed
{
    public Computed? SynchronizationTarget { get; }
}
