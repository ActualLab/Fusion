namespace ActualLab.Pooling;

/// <summary>
/// Defines the contract for a pool that rents and releases resources of type
/// <typeparamref name="T"/>.
/// </summary>
public interface IPool<T> : IResourceReleaser<T>
{
    public ResourceLease<T> Rent();
}
