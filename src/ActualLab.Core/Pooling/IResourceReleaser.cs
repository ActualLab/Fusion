namespace ActualLab.Pooling;

public interface IResourceReleaser<in T>
{
    public bool Release(T resource);
}
