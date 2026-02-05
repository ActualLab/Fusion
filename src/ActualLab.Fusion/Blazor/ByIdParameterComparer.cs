namespace ActualLab.Fusion.Blazor;

/// <summary>
/// A <see cref="ParameterComparer"/> that compares parameters by their <see cref="IHasId{TId}.Id"/>.
/// </summary>
public sealed class ByIdParameterComparer<TId> : ParameterComparer
{
    public static ByIdParameterComparer<TId> Instance { get; } = new();

    public override bool AreEqual(object? oldValue, object? newValue)
    {
        if (ReferenceEquals(oldValue, newValue))
            return true; // Might be the most frequent case
        if (oldValue is null)
            return newValue is null;
        if (newValue is null)
            return false;

        var oldId = ((IHasId<TId>)oldValue).Id;
        var newId = ((IHasId<TId>)newValue).Id;
        return EqualityComparer<TId>.Default.Equals(oldId, newId);
    }
}
