namespace ActualLab.Fusion.Blazor;

/// <summary>
/// Defines the parameter comparison strategy for Blazor Fusion components.
/// </summary>
public enum ParameterComparisonMode
{
    Inherited = 0,
    Custom,
    Standard,
}

/// <summary>
/// Extension methods for <see cref="ParameterComparisonMode"/>.
/// </summary>
public static class ParameterComparisonModeExt
{
    public static ParameterComparisonMode? NullIfInherited(this ParameterComparisonMode mode)
        => mode == ParameterComparisonMode.Inherited ? null : mode;
}
