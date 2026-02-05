namespace ActualLab.Fusion.Blazor;

/// <summary>
/// A <see cref="ParameterComparer"/> that compares parameters by reference equality.
/// </summary>
public sealed class ByRefParameterComparer : ParameterComparer
{
    public static ByRefParameterComparer Instance { get; } = new();

    public override bool AreEqual(object? oldValue, object? newValue)
        => ReferenceEquals(oldValue, newValue);
}
