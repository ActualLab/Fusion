namespace ActualLab.Fusion.Blazor;

public sealed class ByRefParameterComparer : ParameterComparer
{
    public static ByRefParameterComparer Instance { get; } = new();

    public override bool AreEqual(object? oldValue, object? newValue)
        => ReferenceEquals(oldValue, newValue);
}
