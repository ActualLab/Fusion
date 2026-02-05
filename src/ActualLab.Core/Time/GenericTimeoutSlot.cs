namespace ActualLab.Time;

/// <summary>
/// Pairs an <see cref="IGenericTimeoutHandler"/> with an argument for use in timer sets.
/// </summary>
public readonly record struct GenericTimeoutSlot(IGenericTimeoutHandler Handler, object? Argument)
{
    // Equality relies solely on referential equality of a Handler
    public bool Equals(GenericTimeoutSlot other) => ReferenceEquals(Handler, other.Handler);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(Handler);
}
