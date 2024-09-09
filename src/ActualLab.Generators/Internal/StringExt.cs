namespace ActualLab.Generators.Internal;

public static class StringExt
{
    public static int OrdinalIndexOf(this string source, char value)
    {
#if !NETSTANDARD2_0
        return source.IndexOf(value, StringComparison.Ordinal);
#else
        return source.IndexOf(value);
#endif
    }

    public static int OrdinalIndexOf(this string source, string value)
        => source.IndexOf(value, StringComparison.Ordinal);

    public static bool OrdinalStartsWith(this string source, string prefix)
        => source.StartsWith(prefix, StringComparison.Ordinal);

    public static string OrdinalReplace(this string source, string oldValue, string newValue)
    {
#if !NETSTANDARD2_0
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
#else
        return source.Replace(oldValue, newValue);
#endif
    }
}
