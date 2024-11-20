namespace ActualLab.Pooling;

public interface IResourceLease<out T> : IDisposable
{
    public T Resource { get; }
}
