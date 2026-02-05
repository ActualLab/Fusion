namespace ActualLab.Pooling;

/// <summary>
/// Defines the contract for returning a resource of type <typeparamref name="T"/>
/// back to its pool.
/// </summary>
public interface IResourceReleaser<in T>
{
    public bool Release(T resource);
}
