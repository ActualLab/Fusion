namespace ActualLab.Requirements;

/// <summary>
/// A <see cref="Requirement{T}"/> wrapper that delegates satisfaction checks to a
/// base requirement and uses a customizable <see cref="ExceptionBuilder"/> for errors.
/// </summary>
public record CustomizableRequirement<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    (Requirement<T> BaseRequirement) : CustomizableRequirementBase<T>
{
    public CustomizableRequirement(Requirement<T> baseRequirement, ExceptionBuilder exceptionBuilder)
        : this(baseRequirement)
        => ExceptionBuilder = exceptionBuilder;

    public override bool IsSatisfied([NotNullWhen(true)] T? value)
        => BaseRequirement.IsSatisfied(value);

    public override Exception GetError(T? value, string? targetName = null)
        => ExceptionBuilder.Build(value, targetName);
}
