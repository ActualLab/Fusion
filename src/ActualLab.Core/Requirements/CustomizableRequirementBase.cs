namespace ActualLab.Requirements;

/// <summary>
/// Base class for requirements that use an <see cref="ExceptionBuilder"/>
/// to produce validation errors.
/// </summary>
public abstract record CustomizableRequirementBase<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    : Requirement<T>
{
    public ExceptionBuilder ExceptionBuilder { get; init; }

    public override Exception GetError(T? value, string? targetName = null)
        => ExceptionBuilder.Build(value, targetName);
}
