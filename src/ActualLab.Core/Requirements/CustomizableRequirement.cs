using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Requirements;

public record CustomizableRequirement<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    (Requirement<T> BaseRequirement) : CustomizableRequirementBase<T>
{
    public CustomizableRequirement(Requirement<T> baseRequirement, ExceptionBuilder exceptionBuilder)
        : this(baseRequirement)
        => ExceptionBuilder = exceptionBuilder;

    public override bool IsSatisfied([NotNullWhen(true)] T? value)
        => BaseRequirement.IsSatisfied(value);
}
