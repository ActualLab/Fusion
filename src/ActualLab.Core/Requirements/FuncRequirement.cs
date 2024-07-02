using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Requirements;

public record FuncRequirement<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    (Func<T?, bool> Validator) : CustomizableRequirementBase<T>
{
    public FuncRequirement(Func<T?, bool> validator, ExceptionBuilder exceptionBuilder) : this(validator)
        => ExceptionBuilder = exceptionBuilder;

    public override bool IsSatisfied([NotNullWhen(true)] T? value)
        => Validator.Invoke(value);
}
