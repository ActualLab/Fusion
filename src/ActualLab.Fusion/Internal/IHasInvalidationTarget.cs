namespace ActualLab.Fusion.Internal;

/// <summary>
/// An <see cref="IComputed"/> that delegates its invalidation to another <see cref="Computed"/> target.
/// </summary>
public interface IHasInvalidationTarget : IComputed
{
    public Computed? InvalidationTarget { get; }
}
