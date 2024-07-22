namespace ActualLab.Identifiers;

#pragma warning disable CA2252

public interface ISymbolIdentifier : IHasId<Symbol>, ICanBeNone
{
    string Value { get; }
}

#pragma warning disable CA1000

public interface ISymbolIdentifier<TSelf> : ISymbolIdentifier, IEquatable<TSelf>, ICanBeNone<TSelf>
    where TSelf : struct, ISymbolIdentifier<TSelf>
{
#if NET6_0_OR_GREATER
    static abstract TSelf Parse(string? s);
    static abstract TSelf ParseOrNone(string? s);
    static abstract bool TryParse(string? s, out TSelf result);
#endif
}
