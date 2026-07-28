namespace ActualLab.Collections;

/// <summary>
/// Extension methods for <see cref="VersionSet"/>.
/// </summary>
public static class VersionSetExt
{
    public static VersionSet Where(
        this VersionSet source,
        Func<string, Version, bool> predicate)
    {
        var items = new Dictionary<string, Version>(source.Count, StringComparer.Ordinal);
        foreach (var (scope, version) in source.Items)
            if (predicate.Invoke(scope, version))
                items.Add(scope, version);
        return items.Count == source.Count
            ? source
            : items.Count == 0
                ? VersionSet.Empty
                : new VersionSet(items);
    }

    public static VersionSet IntersectScopes(this VersionSet source, HashSet<string> scopes)
        => source.Where((scope, _) => scopes.Contains(scope));
}
