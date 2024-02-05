using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Requirements;

public abstract record CustomizableRequirementBase<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    : Requirement<T>
{
    public ExceptionBuilder ExceptionBuilder { get; init; }

    public override T Check([NotNull] T? value)
    {
        if (!IsSatisfied(value))
            throw ExceptionBuilder.Build(value);
        return value!;
    }
}
