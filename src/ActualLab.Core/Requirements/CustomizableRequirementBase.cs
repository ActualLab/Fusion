using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Requirements;

public abstract record CustomizableRequirementBase<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    : Requirement<T>
{
    public ExceptionBuilder ExceptionBuilder { get; init; }

    public override Exception GetError(T? value)
        => ExceptionBuilder.Build(value);
}
