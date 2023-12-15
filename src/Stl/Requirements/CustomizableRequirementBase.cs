using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Requirements;

public abstract record CustomizableRequirementBase<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    : Requirement<T>
{
    public ExceptionBuilder ExceptionBuilder { get; init; }

#if NETSTANDARD2_0
    public override T Check(T? value)
#else
    public override T Check([NotNull] T? value)
#endif
    {
        if (!IsSatisfied(value))
            throw ExceptionBuilder.Build(value);
        return value!;
    }
}
