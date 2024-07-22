namespace ActualLab.Collections.Internal;

internal static class OptionSetHelper
{
    public static string GetToStringArgs(IReadOnlyDictionary<Symbol, object> items)
    {
        const int limit = 5;
        var count = items.Count;
        if (count == 0)
            return "";

        var args = items.Take(limit).Select(kv => $"{kv.Key}: {kv.Value}").ToDelimitedString();
        return count > limit
            ? $"{count} items: [ {args}, ... ]"
            : $"{count} items: [ {args} ]";
    }

    public static Dictionary<string, NewtonsoftJsonSerialized<object>> ToNewtonsoftJsonCompatible(
        IReadOnlyDictionary<Symbol, object> items)
        => items.ToDictionary(
            p => p.Key.Value,
            p => NewtonsoftJsonSerialized.New(p.Value),
            StringComparer.Ordinal);
}
