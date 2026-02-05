namespace ActualLab.Pooling;

/// <summary>
/// Defines a disposable lease on a pooled resource of type <typeparamref name="T"/>.
/// </summary>
public interface IResourceLease<out T> : IDisposable
{
    public T Resource { get; }
}
