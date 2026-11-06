using System.Text.RegularExpressions;

namespace ActualLab.Rpc.TypeScript;

/// <summary>
/// Selects services by a comma-separated list of type name patterns like <c>"ITodo*,IStockApi"</c>,
/// where <c>*</c> matches any sequence of characters. A pattern matches either the short or the full type name.
/// </summary>
public sealed class RpcTypeScriptServiceFilter
{
    public static RpcTypeScriptServiceFilter All { get; } = new("*");

    private readonly Regex[] _regexes;

    public IReadOnlyList<string> Patterns { get; }

    public RpcTypeScriptServiceFilter(string patterns)
    {
        Patterns = patterns
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (Patterns.Count == 0)
            Patterns = ["*"];
        _regexes = Patterns.Select(NewRegex).ToArray();
    }

    public override string ToString()
        => string.Join(",", Patterns);

    public bool IsMatch(Type type)
        => _regexes.Any(r => IsMatch(r, type));

    public IEnumerable<string> GetUnmatchedPatterns(IReadOnlyCollection<Type> types)
        => Patterns.Where((_, i) => !types.Any(t => IsMatch(_regexes[i], t)));

    // Private methods

    private static bool IsMatch(Regex regex, Type type)
        => regex.IsMatch(type.Name) || (type.FullName is { } fullName && regex.IsMatch(fullName));

    private static Regex NewRegex(string pattern)
    {
        var parts = pattern.Split('*').Select(Regex.Escape);
        return new Regex($"^{string.Join(".*", parts)}$", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }
}
