namespace ActualLab.Fusion.Blazor;

public sealed class ByValueParameterComparer : ParameterComparer
{
    public static ByUuidParameterComparer Instance { get; } = new();

    public override bool AreEqual(object? oldValue, object? newValue)
        => Equals(oldValue, newValue);
}
