using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Requirements;

public record ServiceExceptionWrapper<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    (Requirement<T> BaseRequirement) : Requirement<T>
{
    public static readonly ServiceExceptionWrapper<T> Default =
        new(MustExistRequirement<T>.Default);

    public override bool IsSatisfied(T? value)
        => BaseRequirement.IsSatisfied(value);

    public override T Check([NotNull] T? value)
    {
        try {
            return BaseRequirement.Check(value);
        }
        catch (Exception e) when (e is not OperationCanceledException) {
            throw new ServiceException(e.Message, e);
        }
    }
}
