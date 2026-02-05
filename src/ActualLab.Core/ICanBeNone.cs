namespace ActualLab;

#pragma warning disable CA2252

/// <summary>
/// Indicates that the implementing type has a "none" or empty state.
/// </summary>
public interface ICanBeNone
{
    public bool IsNone { get; }
}

/// <summary>
/// Strongly typed version of <see cref="ICanBeNone"/> that exposes a static <c>None</c> value.
/// </summary>
public interface ICanBeNone<out TSelf> : ICanBeNone
    where TSelf : ICanBeNone<TSelf>
{
#if NET6_0_OR_GREATER
    public static abstract TSelf None { get; }
#endif
}
