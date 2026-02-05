namespace ActualLab.Comparison;

/// <summary>
/// An equality comparer for <see cref="IHasId{TId}"/> that compares by <see cref="IHasId{TId}.Id"/>.
/// </summary>
public class HasIdEqualityComparer<T> : IEqualityComparer<IHasId<T>>
{
    public static readonly IEqualityComparer<IHasId<T>> Instance = new HasIdEqualityComparer<T>();

    public bool Equals(IHasId<T>? x, IHasId<T>? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null)
            return y is null;
        return y is not null && EqualityComparer<T>.Default.Equals(x.Id, y.Id);
    }

    public int GetHashCode(IHasId<T>? obj)
        => obj is null ? 0 : EqualityComparer<T>.Default.GetHashCode(obj.Id!);
}
