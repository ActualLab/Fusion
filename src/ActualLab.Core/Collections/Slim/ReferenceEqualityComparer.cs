namespace ActualLab.Collections.Slim;

/// <summary>
/// An <see cref="IEqualityComparer{T}"/> that compares reference type instances
/// by reference identity rather than value equality.
/// </summary>
public sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    where T : class
{
    public static readonly ReferenceEqualityComparer<T> Instance = new();

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}
