namespace ActualLab.Requirements;

public record JointRequirement<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    (Requirement<T> Primary, Requirement<T> Secondary) : Requirement<T>
{
    public override bool IsSatisfied([NotNullWhen(true)] T? value)
        => Primary.IsSatisfied(value) && Secondary.IsSatisfied(value);

    public override void Check([NotNull] T? value, string? targetName = null)
    {
        Primary.Check(value, targetName);
        Secondary.Check(value, targetName);
    }

    public override Exception GetError(T? value, string? targetName = null)
        => Primary.IsSatisfied(value)
            ? Secondary.GetError(value, targetName)
            : Primary.GetError(value, targetName);
}
