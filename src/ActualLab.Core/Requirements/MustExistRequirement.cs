using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Requirements;

public record MustExistRequirement<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    : CustomizableRequirementBase<T>
{
    public static readonly MustExistRequirement<T> Default = new();

    public MustExistRequirement()
        => ExceptionBuilder = new("'{0}' is not found.", message => new ValidationException(message));

    public override bool IsSatisfied([NotNullWhen(true)] T? value)
        => typeof(T).IsValueType
            ? !EqualityComparer<T>.Default.Equals(value!, default!)
            : !ReferenceEquals(value, null);
}
