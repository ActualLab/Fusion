using ActualLab.Internal;

namespace ActualLab.Requirements;

/// <summary>
/// Marker interface for must-exist requirements.
/// </summary>
public interface IMustExistRequirement;

/// <summary>
/// A requirement that checks a value is not null or default.
/// </summary>
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

/// <summary>
/// Non-generic helper methods for <see cref="MustExistRequirement{T}"/>.
/// </summary>
public static class MustExistRequirement
{
    public static bool IsSatisfied<T>([NotNullWhen(true)] T? value)
        => typeof(T).IsValueType
            ? !EqualityComparer<T>.Default.Equals(value!, default!)
            : !ReferenceEquals(value, null);
}
