namespace ActualLab.Collections.Internal;

/// <summary>
/// Helper methods for <see cref="OptionSet"/> and <see cref="ImmutableOptionSet"/>
/// string formatting and serialization.
/// </summary>
internal static class OptionSetHelper
{
    public static string GetToStringArgs(IReadOnlyDictionary<string, object> items)
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

    public static IDictionary<string, NewtonsoftJsonSerialized<object>> ToNewtonsoftJsonCompatible(
        IReadOnlyDictionary<string, object> items)
    {
        var result = new SortedDictionary<string, NewtonsoftJsonSerialized<object>>(StringComparer.Ordinal);
        foreach (var (key, value) in items)
            result.Add(key, NewtonsoftJsonSerialized.New(value));
        return result;
    }
}
