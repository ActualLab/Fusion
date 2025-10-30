namespace ActualLab.Fusion.Internal;

public interface IInvalidationProxyComputed : IComputed
{
    public Computed? InvalidationTarget { get; }
}
