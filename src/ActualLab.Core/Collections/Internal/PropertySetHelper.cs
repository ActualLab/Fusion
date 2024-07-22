namespace ActualLab.Collections.Internal;

internal static class PropertySetHelper
{
    public static string GetToStringArgs(IReadOnlyDictionary<Symbol, TypeDecoratingUniSerialized<object>> items)
    {
        const int limit = 5;
        var count = items.Count;
        if (count == 0)
            return "";

        var args = items.Take(limit).Select(kv => $"{kv.Key}: {kv.Value.Value}").ToDelimitedString();
        return count > limit
            ? $"{count} items: [ {args}, ... ]"
            : $"{count} items: [ {args} ]";
    }
}
