using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Requirements;

public interface IMustExistRequirement;

public sealed record MustExistRequirement<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    : Requirement<T>, IMustExistRequirement
{
    private static readonly string ErrorMessage = $"{typeof(T).GetName()} is required here.";

    public static readonly MustExistRequirement<T> Default = new();

    public override bool IsSatisfied([NotNullWhen(true)] T? value)
        => MustExistRequirement.IsSatisfied(value);

    public override Exception GetError(T? value)
        => Errors.Constraint(ErrorMessage);
}

public static class MustExistRequirement
{
    public static bool IsSatisfied<T>([NotNullWhen(true)] T? value)
        => typeof(T).IsValueType
            ? !EqualityComparer<T>.Default.Equals(value!, default!)
            : !ReferenceEquals(value, null);
}
