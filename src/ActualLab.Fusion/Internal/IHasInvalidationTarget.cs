namespace ActualLab.Fusion.Internal;

public interface IHasInvalidationTarget : IComputed
{
    public Computed? InvalidationTarget { get; }
}
