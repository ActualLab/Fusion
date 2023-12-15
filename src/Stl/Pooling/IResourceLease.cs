namespace ActualLab.Pooling;

public interface IResourceLease<out T> : IDisposable
{
    T Resource { get; }
}
