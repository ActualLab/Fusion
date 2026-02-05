namespace ActualLab.Fusion.Blazor;

/// <summary>
/// A <see cref="ParameterComparer"/> that always considers parameters equal (never triggers re-render).
/// </summary>
public sealed class ByNoneParameterComparer : ParameterComparer
{
    public static ByNoneParameterComparer Instance { get; } = new();

    public override bool AreEqual(object? oldValue, object? newValue)
        => true;
}
