using ActualLab.Internal;

namespace ActualLab.Identifiers;

public static class CanBeNoneExt
{
    public static T Require<T>(this T source)
        where T : ICanBeNone
        => source.Require(typeof(T).GetName());
    public static T Require<T>(this T source, string name)
        where T : ICanBeNone
        => source.IsNone ? throw Errors.Constraint($"{name} is required here.") : source;

    public static T RequireNone<T>(this T source)
        where T : ICanBeNone
        => source.RequireNone(typeof(T).GetName());
    public static T RequireNone<T>(this T source, string name)
        where T : ICanBeNone
        => source.IsNone ? source : throw Errors.Constraint($"{name} must be None here.");

    public static TId Or<TId>(this TId value, TId noneReplacementValue)
        where TId : ICanBeNone
        => !value.IsNone ? value : noneReplacementValue;

    public static TId Or<TId>(this TId value, Func<TId> noneReplacementFactory)
        where TId : ICanBeNone
        => !value.IsNone ? value : noneReplacementFactory.Invoke();

    public static TId Or<TId, TState>(this TId value, TState state, Func<TState, TId> noneReplacementFactory)
        where TId : ICanBeNone
        => !value.IsNone ? value : noneReplacementFactory.Invoke(state);
}
