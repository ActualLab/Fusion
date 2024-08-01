using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Requirements;

public interface IMustExistRequirement;

public sealed record MustExistRequirement<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    : CustomizableRequirementBase<T>, IMustExistRequirement
{
    public readonly ExceptionBuilder DefaultExceptionBuilder
        = new("'{0}' is not found.", typeof(T).GetName(), Errors.Constraint);

    public static readonly MustExistRequirement<T> Default = new();

    public MustExistRequirement()
        => ExceptionBuilder = DefaultExceptionBuilder;

    public override bool IsSatisfied([NotNullWhen(true)] T? value)
        => MustExistRequirement.IsSatisfied(value);
}

public static class MustExistRequirement
{
    public static bool IsSatisfied<T>([NotNullWhen(true)] T? value)
        => typeof(T).IsValueType
            ? !EqualityComparer<T>.Default.Equals(value!, default!)
            : !ReferenceEquals(value, null);
}
