namespace ActualLab.Pooling;

public interface IPool<T> : IResourceReleaser<T>
{
    public ResourceLease<T> Rent();
}
