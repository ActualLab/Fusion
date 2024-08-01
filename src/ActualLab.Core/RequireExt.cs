using System.Diagnostics.CodeAnalysis;

namespace ActualLab;

// It's fine to disable it, coz the matching set of Requirement<T> fields/props must be declared @ T
#pragma warning disable IL2091

public static class RequireExt
{
    // Require w/ implicit MustExistRequirement for classes

    public static T Require<T>([NotNull] this T? target)
    {
        var mustThrow = typeof(T).IsValueType
            ? EqualityComparer<T>.Default.Equals(target, default)
            : ReferenceEquals(target, null);
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting.
        return mustThrow
            ? throw Requirement<T>.MustExist.GetError(target)
            : target!;
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.
    }

    public static async Task<T> Require<T>(this Task<T?> targetSource)
    {
        var target = await targetSource.ConfigureAwait(false);
        Requirement<T>.MustExist.Check(target);
        return target;
    }

    public static async ValueTask<T> Require<T>(this ValueTask<T?> targetSource)
    {
        var target = await targetSource.ConfigureAwait(false);
        Requirement<T>.MustExist.Check(target);
        return target;
    }

    // Require w/ explicit Requirement

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Require<T>([NotNull] this T? target, Requirement<T> requirement)
    {
        requirement.Check(target);
        return target;
    }

    public static async Task<T> Require<T>(this Task<T?> targetSource, Requirement<T> requirement)
    {
        var target = await targetSource.ConfigureAwait(false);
        requirement.Check(target);
        return target;
    }

    public static async ValueTask<T> Require<T>(this ValueTask<T?> targetSource, Requirement<T> requirement)
    {
        var target = await targetSource.ConfigureAwait(false);
        requirement.Check(target);
        return target;
    }

    // Require w/ requirement builder

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Require<T>([NotNull] this T? target, Func<Requirement<T>> requirementBuilder)
        where T : IRequirementTarget
    {
        requirementBuilder.Invoke().Check(target);
        return target;
    }

    public static async Task<T> Require<T>(this Task<T?> targetSource, Func<Requirement<T>> requirementBuilder)
        where T : IRequirementTarget
    {
        var target = await targetSource.ConfigureAwait(false);
        requirementBuilder.Invoke().Check(target);
        return target;
    }

    public static async ValueTask<T> Require<T>(this ValueTask<T?> targetSource, Func<Requirement<T>> requirementBuilder)
        where T : IRequirementTarget
    {
        var target = await targetSource.ConfigureAwait(false);
        requirementBuilder.Invoke().Check(target);
        return target;
    }
}
