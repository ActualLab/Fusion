namespace ActualLab.Identifiers;

#pragma warning disable CA2252

public interface ICanBeNone
{
    public bool IsNone { get; }
}

public interface ICanBeNone<out TSelf> : ICanBeNone
    where TSelf : ICanBeNone<TSelf>
{
#if NET6_0_OR_GREATER
    public static abstract TSelf None { get; }
#endif
}
