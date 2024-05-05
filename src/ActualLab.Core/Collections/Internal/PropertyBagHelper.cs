namespace ActualLab.Collections.Internal;

internal static class PropertyBagHelper
{
    public static string GetToStringArgs(PropertyBagItem[]? items)
    {
        const int limit = 5;
        var count = items?.Length ?? 0;
        if (count == 0)
            return "";

        var args = items!.Take(limit).Select(x => $"{x.Key}: {x.Value}").ToDelimitedString();
        return count > limit
            ? $"{count} items: [ {args}, ... ]"
            : $"{count} items: [ {args} ]";
    }
}
