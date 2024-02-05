using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Requirements;

public record JointRequirement<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    (Requirement<T> Primary, Requirement<T> Secondary) : Requirement<T>
{
    public override bool IsSatisfied([NotNullWhen(true)] T? value)
        => Primary.IsSatisfied(value) && Secondary.IsSatisfied(value);

    public override T Check([NotNull] T? value)
        => Secondary.Check(Primary.Check(value));
}
