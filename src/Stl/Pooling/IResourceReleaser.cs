namespace ActualLab.Pooling;

public interface IResourceReleaser<in T>
{
    bool Release(T resource);
}
