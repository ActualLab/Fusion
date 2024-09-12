namespace ActualLab.Fusion.Blazor;

public sealed class ByNoneParameterComparer : ParameterComparer
{
    public static ByNoneParameterComparer Instance { get; } = new();

    public override bool AreEqual(object? oldValue, object? newValue)
        => true;
}
