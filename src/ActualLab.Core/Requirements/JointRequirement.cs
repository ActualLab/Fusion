using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Requirements;

public record JointRequirement<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    (Requirement<T> Primary, Requirement<T> Secondary) : Requirement<T>
{
    public override bool IsSatisfied([NotNullWhen(true)] T? value)
        => Primary.IsSatisfied(value) && Secondary.IsSatisfied(value);

    public override void Check([NotNull] T? value)
    {
        Primary.Check(value);
        Secondary.Check(value);
    }

    public override Exception GetError(T? value)
        => Primary.IsSatisfied(value)
            ? Secondary.GetError(value)
            : Primary.GetError(value);
}
