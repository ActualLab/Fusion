namespace ActualLab;

// It's fine to disable it, coz the matching set of Requirement<T> fields/props must be declared @ T
public static class RequireExt
{
    // Require w/ implicit MustExistRequirement for classes

    public static T Require<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        [NotNull] this T? target, string? targetName = null)
    {
        var mustThrow = typeof(T).IsValueType
            ? EqualityComparer<T>.Default.Equals(target!, default!)
            : ReferenceEquals(target, null);
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting.
        return mustThrow
            ? throw Requirement<T>.MustExist.GetError(target, targetName)
            : target!;
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.
    }

    public static async Task<T> Require<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        this Task<T?> targetSource, string? targetName = null)
    {
        var target = await targetSource.ConfigureAwait(false);
        Requirement<T>.MustExist.Check(target, targetName);
        return target;
    }

    public static async ValueTask<T> Require<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        this ValueTask<T?> targetSource, string? targetName = null)
    {
        var target = await targetSource.ConfigureAwait(false);
        Requirement<T>.MustExist.Check(target, targetName);
        return target;
    }

    // Require w/ explicit Requirement

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Require<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        [NotNull] this T? target, Requirement<T> requirement, string? targetName = null)
    {
        requirement.Check(target, targetName);
        return target;
    }

    public static async Task<T> Require<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        this Task<T?> targetSource, Requirement<T> requirement, string? targetName = null)
    {
        var target = await targetSource.ConfigureAwait(false);
        requirement.Check(target, targetName);
        return target;
    }

    public static async ValueTask<T> Require<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        this ValueTask<T?> targetSource, Requirement<T> requirement, string? targetName = null)
    {
        var target = await targetSource.ConfigureAwait(false);
        requirement.Check(target, targetName);
        return target;
    }

    // Require w/ requirement builder

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Require<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        [NotNull] this T? target, Func<Requirement<T>> requirementBuilder, string? targetName = null)
        where T : IRequirementTarget
    {
        requirementBuilder.Invoke().Check(target, targetName);
        return target;
    }

    public static async Task<T> Require<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        this Task<T?> targetSource, Func<Requirement<T>> requirementBuilder, string? targetName = null)
        where T : IRequirementTarget
    {
        var target = await targetSource.ConfigureAwait(false);
        requirementBuilder.Invoke().Check(target, targetName);
        return target;
    }

    public static async ValueTask<T> Require<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        this ValueTask<T?> targetSource, Func<Requirement<T>> requirementBuilder, string? targetName = null)
        where T : IRequirementTarget
    {
        var target = await targetSource.ConfigureAwait(false);
        requirementBuilder.Invoke().Check(target, targetName);
        return target;
    }
}
