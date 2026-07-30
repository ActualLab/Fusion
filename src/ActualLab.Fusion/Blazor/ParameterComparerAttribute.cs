namespace ActualLab.Fusion.Blazor;

#pragma warning disable CA1813 // Consider making sealed

/// <summary>
/// Specifies a custom <see cref="ParameterComparer"/> type for a Blazor component parameter
/// or a type used as a parameter.
/// </summary>
[AttributeUsage(
    AttributeTargets.Interface |
    AttributeTargets.Class |
    AttributeTargets.Struct |
    AttributeTargets.Enum |
    AttributeTargets.Delegate |
    AttributeTargets.Property)]
public class ParameterComparerAttribute(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type comparerType
    ) : Attribute
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type ComparerType { get; } = comparerType;
}
