namespace ActualLab.Pooling;

[StructLayout(LayoutKind.Auto)]
public readonly struct ResourceLease<T>(T resource, IResourceReleaser<T> releaser)
    : IResourceLease<T>, IEquatable<ResourceLease<T>>
{
    private readonly IResourceReleaser<T> _releaser = releaser;

    public T Resource { get; } = resource;

    public void Dispose()
    {
        if (_releaser?.Release(Resource) ?? false)
            return;
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        if (Resource is IDisposable d)
            d.Dispose();
    }

    public override string ToString() => $"{GetType().GetName()}({Resource})";

    // Equality

    public bool Equals(ResourceLease<T> other)
        => ReferenceEquals(_releaser, other._releaser)
            && EqualityComparer<T>.Default.Equals(Resource, other.Resource);
    public override bool Equals(object? obj) => obj is ResourceLease<T> other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Resource, _releaser);
    public static bool operator ==(ResourceLease<T> left, ResourceLease<T> right) => left.Equals(right);
    public static bool operator !=(ResourceLease<T> left, ResourceLease<T> right) => !left.Equals(right);
}
