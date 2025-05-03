namespace ActualLab.Comparison;

// Shouldn't be serializable!
public readonly struct ByRef<T>(T target) : IEquatable<ByRef<T>>
    where T : class?
{
    public T Target { get; } = target;

    public override string ToString()
        => $"{GetType().GetName()}({Target?.ToString() ?? "␀"})";

    // Equality
    public bool Equals(ByRef<T> other)
        => ReferenceEquals(Target, other.Target);
    public override bool Equals(object? obj)
        => obj is ByRef<T> other && Equals(other);
    public override int GetHashCode()
        => RuntimeHelpers.GetHashCode(Target!);
    public static bool operator ==(ByRef<T> left, ByRef<T> right)
        => left.Equals(right);
    public static bool operator !=(ByRef<T> left, ByRef<T> right)
        => !left.Equals(right);
}

public static class ByRef
{
    public static ByRef<T> New<T>(T value)
        where T : class?
        => new(value);
}
