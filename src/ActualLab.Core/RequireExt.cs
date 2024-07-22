using System.Diagnostics.CodeAnalysis;
using ActualLab.Requirements;

namespace ActualLab;

// It's fine to disable it, coz the matching set of Requirement<T> fields/props must be declared @ T
#pragma warning disable IL2091

public static class RequireExt
{
    // Require w/ implicit MustExistRequirement

    public static T Require<T>([NotNull] this T? target)
        => MustExistRequirement.IsSatisfied(target)
            ? target
            : Requirement<T>.MustExist.Check(target);

    public static async Task<T> Require<T>(this Task<T?> targetSource)
    {
        var target = await targetSource.ConfigureAwait(false);
        return MustExistRequirement.IsSatisfied(target)
            ? target
            : Requirement<T>.MustExist.Check(target);
    }

    public static async ValueTask<T> Require<T>(this ValueTask<T?> targetSource)
    {
        var target = await targetSource.ConfigureAwait(false);
        return MustExistRequirement.IsSatisfied(target)
            ? target
            : Requirement<T>.MustExist.Check(target);
    }

    // Require w/ explicit Requirement

    public static T Require<T>([NotNull] this T? target, Requirement<T>? requirement)
    {
        requirement ??= Requirement<T>.MustExist;
        return requirement.Check(target);
    }

    public static async Task<T> Require<T>(this Task<T?> targetSource, Requirement<T>? requirement)
    {
        var target = await targetSource.ConfigureAwait(false);
        requirement ??= Requirement<T>.MustExist;
        return requirement.Check(target);
    }

    public static async ValueTask<T> Require<T>(this ValueTask<T?> targetSource, Requirement<T>? requirement)
    {
        var target = await targetSource.ConfigureAwait(false);
        requirement ??= Requirement<T>.MustExist;
        return requirement.Check(target);
    }

    // Require w/ requirement builder

    public static T Require<T>([NotNull] this T? target, Func<Requirement<T>> requirementBuilder)
        where T : IRequirementTarget
        => requirementBuilder.Invoke().Check(target);

    public static async Task<T> Require<T>(this Task<T?> targetSource, Func<Requirement<T>> requirementBuilder)
        where T : IRequirementTarget
    {
        var target = await targetSource.ConfigureAwait(false);
        return requirementBuilder.Invoke().Check(target);
    }

    public static async ValueTask<T> Require<T>(this ValueTask<T?> targetSource, Func<Requirement<T>> requirementBuilder)
        where T : IRequirementTarget
    {
        var target = await targetSource.ConfigureAwait(false);
        return requirementBuilder.Invoke().Check(target);
    }
}
