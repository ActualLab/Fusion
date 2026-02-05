namespace ActualLab.Fusion.Blazor;

/// <summary>
/// A <see cref="ParameterComparer"/> that compares parameters using <see cref="object.Equals(object, object)"/>.
/// </summary>
public sealed class ByValueParameterComparer : ParameterComparer
{
    public static ByUuidParameterComparer Instance { get; } = new();

    public override bool AreEqual(object? oldValue, object? newValue)
        => Equals(oldValue, newValue);
}
