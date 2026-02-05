namespace ActualLab.Fusion.Blazor;

/// <summary>
/// Base class for custom Blazor component parameter comparers used to determine
/// whether a component should re-render.
/// </summary>
public abstract class ParameterComparer
{
    public abstract bool AreEqual(object? oldValue, object? newValue);
}
