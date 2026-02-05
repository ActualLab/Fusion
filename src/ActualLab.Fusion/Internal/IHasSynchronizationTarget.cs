namespace ActualLab.Fusion.Internal;

/// <summary>
/// An <see cref="IComputed"/> that delegates its synchronization to another <see cref="Computed"/> target.
/// </summary>
public interface IHasSynchronizationTarget : IComputed
{
    public Computed? SynchronizationTarget { get; }
}
