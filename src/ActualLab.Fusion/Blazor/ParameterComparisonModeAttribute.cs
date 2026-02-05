namespace ActualLab.Fusion.Blazor;

#pragma warning disable CA1813 // Consider making sealed

/// <summary>
/// Specifies the <see cref="ParameterComparisonMode"/> for a Blazor Fusion component class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class FusionComponentAttribute(ParameterComparisonMode parameterComparisonMode) : Attribute
{
    public ParameterComparisonMode ParameterComparisonMode { get; } = parameterComparisonMode;
}
