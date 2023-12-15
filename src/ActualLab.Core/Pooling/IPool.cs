namespace ActualLab.Pooling;

public interface IPool<T> : IResourceReleaser<T>
{
    ResourceLease<T> Rent();
}
