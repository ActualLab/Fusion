namespace ActualLab.Identifiers;

public static class SymbolIdentifier
{
    public static T Parse<T>(string? s)
        where T : struct, ISymbolIdentifier<T>
#if NET6_0_OR_GREATER
        => T.Parse(s);
#else
        => ParseCache<T>.ParseFunc.Invoke(s);
#endif

    public static T ParseOrNone<T>(string? s)
        where T : struct, ISymbolIdentifier<T>
#if NET6_0_OR_GREATER
        => T.ParseOrNone(s);
#else
        => ParseCache<T>.ParseOrNoneFunc.Invoke(s);
#endif

#if !NET6_0_OR_GREATER
    private static class ParseCache<T>
    {
        public static readonly Func<string?, T> ParseFunc = (Func<string?, T>)typeof(T)
            .GetMethod(nameof(Parse), BindingFlags.Public | BindingFlags.Static, null, [ typeof(string) ], null)!
            .CreateDelegate(typeof(Func<string?, T>));
        public static readonly Func<string?, T> ParseOrNoneFunc = (Func<string?, T>)typeof(T)
            .GetMethod(nameof(ParseOrNone), BindingFlags.Public | BindingFlags.Static, null, [ typeof(string) ], null)!
            .CreateDelegate(typeof(Func<string?, T>));
    }
#endif
}
