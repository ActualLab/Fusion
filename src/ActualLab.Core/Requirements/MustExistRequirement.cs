using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Requirements;

public record MustExistRequirement<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    : CustomizableRequirementBase<T>
{
    public static readonly MustExistRequirement<T> Default = new();

    public MustExistRequirement()
        => ExceptionBuilder = new("'{0}' is not found.", Errors.Constraint);

    public override bool IsSatisfied([NotNullWhen(true)] T? value)
        => typeof(T).IsValueType
            ? !EqualityComparer<T>.Default.Equals(value!, default!)
            : !ReferenceEquals(value, null);
}
