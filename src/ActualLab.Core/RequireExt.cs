using System.Diagnostics.CodeAnalysis;

namespace ActualLab;

// It's fine to disable it, coz the matching set of Requirement<T> fields/props must be declared @ T
#pragma warning disable IL2091

public static class RequireExt
{
    // Normal overloads

    public static T Require<T>([NotNull] this T? target, Requirement<T>? requirement = null)
    {
        requirement ??= Requirement<T>.MustExist;
        return requirement.Check(target);
    }

    public static async Task<T> Require<T>(this Task<T?> targetSource, Requirement<T>? requirement = null)
    {
        var target = await targetSource.ConfigureAwait(false);
        requirement ??= Requirement<T>.MustExist;
        return requirement.Check(target);
    }

    public static async ValueTask<T> Require<T>(this ValueTask<T?> targetSource, Requirement<T>? requirement = null)
    {
        var target = await targetSource.ConfigureAwait(false);
        requirement ??= Requirement<T>.MustExist;
        return requirement.Check(target);
    }

    // Overloads accepting requirement builder

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
