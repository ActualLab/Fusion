namespace ActualLab.Text;

public static class StringExt
{
    public static string ManyToOne(IEnumerable<string> values) => ManyToOne(values, ListFormat.Default);
    public static string ManyToOne(IEnumerable<string> values, ListFormat listFormat)
    {
        using var f = listFormat.CreateFormatter();
        foreach (var value in values)
            f.Append(value);
        f.AppendEnd();
        return f.OutputBuilder.ToString();
    }

    public static string[] OneToMany(string value) => OneToMany(value, ListFormat.Default);
    public static string[] OneToMany(string value, ListFormat listFormat)
    {
        if (value.IsNullOrEmpty())
            return [];

        using var p = listFormat.CreateParser(value);
        var buffer = MemoryBuffer<string>.Lease(true);
        try {
            while (p.TryParseNext())
                buffer.Add(p.Item);
            return buffer.ToArray();
        }
        finally {
            buffer.Release();
        }
    }

    public static string TrimSuffixes(this string source, params ReadOnlySpan<string> suffixes)
    {
        foreach (var suffix in suffixes) {
            var result = source.TrimSuffix(suffix);
            if (!ReferenceEquals(result, source))
                return result;
        }
        return source;
    }

    public static string TrimSuffix(this string source, string suffix)
        => source.EndsWith(suffix, StringComparison.Ordinal)
            ? source.Substring(0, source.Length - suffix.Length)
            : source;

    public static ulong GetXxHash3L(this string source)
        => source.AsSpan().GetXxHash3L();

    public static int GetXxHash3(this string source)
        => source.AsSpan().GetXxHash3();
}
