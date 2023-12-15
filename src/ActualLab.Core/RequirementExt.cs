using System.Diagnostics.CodeAnalysis;
using ActualLab.Requirements;

namespace ActualLab;

public static class RequirementExt
{
    public static Requirement<T> WithServiceException<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
        (this Requirement<T> requirement)
    {
        if (requirement is ServiceExceptionWrapper<T>)
            return requirement;

        return ReferenceEquals(requirement, MustExistRequirement<T>.Default)
            ? ServiceExceptionWrapper<T>.Default
            : new ServiceExceptionWrapper<T>(requirement);
    }
}
